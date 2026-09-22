using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

// Owns LRCLIB transport and response reuse, not recording identity or lyrics selection.
internal sealed class LrclibClient : IDisposable
{
	private sealed record CachedJsonResponse(string Json, DateTimeOffset ExpiresAtUtc)
	{
		public ConcurrentDictionary<string, byte> TrackKeys { get; } = new(StringComparer.Ordinal);
	}

	private static readonly TimeSpan RequestCacheLifetime = TimeSpan.FromMinutes(10L);

	private readonly HttpClient _httpClient;
	private readonly AppLogger _logger;

	private readonly ConcurrentDictionary<string, CachedJsonResponse> _requestCache = new ConcurrentDictionary<string, CachedJsonResponse>(StringComparer.Ordinal);

	private readonly ConcurrentDictionary<string, SemaphoreSlim> _requestGates = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);

	private readonly object _serverBackoffLock = new object();

	private DateTimeOffset _serverBackoffUntilUtc;

	private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	public LrclibClient(AppLogger logger, HttpMessageHandler? messageHandler)
	{
		_logger = logger;
		HttpMessageHandler handler = messageHandler ?? new HttpClientHandler
		{
			AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli),
			UseCookies = false,
			MaxConnectionsPerServer = 2
		};
		_httpClient = new HttpClient(handler)
		{
			BaseAddress = new Uri("https://lrclib.net/"),
			Timeout = Timeout.InfiniteTimeSpan
		};
		_httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FlowLyrics", BuildInfo.Version));
		_httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://github.com/MoriKouta/FlowLyrics)"));
		_httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
	}

	public string GetAbsoluteUrl(string relativeUrl) => new Uri(_httpClient.BaseAddress!, relativeUrl).AbsoluteUri;

	public void InvalidateTrack(string trackKey)
	{
		foreach (var response in _requestCache)
			if (response.Value.TrackKeys.ContainsKey(trackKey)) _requestCache.TryRemove(response.Key, out _);
	}

	public void Dispose() => _httpClient.Dispose();

	public async Task<T?> GetJsonAsync<T>(string relativeUrl, int maxAttempts, CancellationToken cancellationToken, bool throwOnNotFound = false, bool bypassRequestCache = false, string? cacheOwner = null)
	{
		if (!bypassRequestCache && TryReadRequestCache<T>(relativeUrl, out T value))
		{
			await _logger.WriteAsync("request-cache hit");
			AssociateRequest(cacheOwner, relativeUrl);
			return value;
		}
		if (bypassRequestCache) await _logger.WriteAsync("request-cache bypass reason=explicit-refresh url=" + relativeUrl);
		SemaphoreSlim gate = _requestGates.GetOrAdd(relativeUrl, _ => new SemaphoreSlim(1, 1));
		await gate.WaitAsync(cancellationToken);
		try
		{
			if (!bypassRequestCache && TryReadRequestCache<T>(relativeUrl, out T cached))
			{
				AssociateRequest(cacheOwner, relativeUrl);
				return cached;
			}
			T? result = await GetJsonWithRetryCoreAsync<T>(relativeUrl, maxAttempts, cancellationToken, throwOnNotFound);
			AssociateRequest(cacheOwner, relativeUrl);
			return result;
		}
		finally
		{
			gate.Release();
		}
	}

	public void AssociateRequest(string? trackKey, string relativeUrl)
	{
		if (trackKey != null && _requestCache.TryGetValue(relativeUrl, out CachedJsonResponse? response))
			response.TrackKeys[trackKey] = 0;
	}

	private async Task<T?> GetJsonWithRetryCoreAsync<T>(string relativeUrl, int maxAttempts, CancellationToken cancellationToken, bool throwOnNotFound)
	{
		Exception? lastError = null;
		int attempts = Math.Clamp(maxAttempts, 1, 3);
		string requestUrl = GetAbsoluteUrl(relativeUrl);
		for (int attempt = 0; attempt < attempts; attempt++)
		{
			await WaitForServerBackoffAsync(cancellationToken);
			using CancellationTokenSource requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			requestCancellation.CancelAfter((attempt == 0) ? TimeSpan.FromSeconds(30L) : TimeSpan.FromSeconds(45L));
			Stopwatch requestTimer = Stopwatch.StartNew();
			await _logger.WriteAsync($"http start method=GET url={requestUrl} attempt={attempt + 1}");
			int? statusCode = null;
			string contentType = "unavailable";
			string responsePrefix = string.Empty;
			try
			{
				using HttpResponseMessage response = await _httpClient.GetAsync(relativeUrl, HttpCompletionOption.ResponseHeadersRead, requestCancellation.Token);
				statusCode = (int)response.StatusCode;
				contentType = response.Content.Headers.ContentType?.ToString() ?? "unavailable";
				string responseBody = await response.Content.ReadAsStringAsync(requestCancellation.Token);
				responsePrefix = BodyPrefix(responseBody);
				string retryAfter = response.Headers.RetryAfter?.ToString() ?? "none";
				await _logger.WriteAsync($"http method=GET url={requestUrl} status={statusCode} contentType={Safe(contentType)} retryAfter={Safe(retryAfter)} attempt={attempt + 1} elapsedMs={requestTimer.ElapsedMilliseconds} bodyPrefix={responsePrefix}");
				if (response.StatusCode == HttpStatusCode.NotFound)
				{
					if (throwOnNotFound)
					{
						throw new LyricsServiceException(LyricsErrorKind.NotFound, LocalizationService.TranslateCurrent("LRCLIB record was not found (404)."));
					}
					return default(T);
				}
				if (IsTransientStatus(response.StatusCode))
				{
					TimeSpan retryDelay = GetRetryDelay(response, attempt);
					SetServerBackoff(retryDelay);
					lastError = response.StatusCode == HttpStatusCode.TooManyRequests
						? new LyricsServiceException(LyricsErrorKind.RateLimited, LocalizationService.TranslateCurrent("LRCLIB communication failed."))
						: new LyricsServiceException(LyricsErrorKind.Network, LocalizationService.TranslateCurrent("LRCLIB communication failed."));
					if (attempt + 1 < attempts)
					{
						await WaitForServerBackoffAsync(cancellationToken);
						continue;
					}
					break;
				}
				response.EnsureSuccessStatusCode();
				T? result = JsonSerializer.Deserialize<T>(responseBody, _jsonOptions);
				CachedJsonResponse refreshed = new(responseBody, DateTimeOffset.UtcNow.Add(RequestCacheLifetime));
				_requestCache.AddOrUpdate(relativeUrl, refreshed, (_, previous) =>
				{
					foreach (string owner in previous.TrackKeys.Keys) refreshed.TrackKeys[owner] = 0;
					return refreshed;
				});
				TrimRequestCache();
				return result;
			}
			catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
			{
				await LogHttpExceptionAsync(requestUrl, statusCode, contentType, responsePrefix, ex, attempt + 1, requestTimer.ElapsedMilliseconds);
				lastError = new LyricsServiceException(LyricsErrorKind.Timeout, LocalizationService.TranslateCurrent("LRCLIB communication failed."), ex);
				SetServerBackoff(GetClientRetryDelay(attempt));
			}
			catch (JsonException ex)
			{
				await LogHttpExceptionAsync(requestUrl, statusCode, contentType, responsePrefix, ex, attempt + 1, requestTimer.ElapsedMilliseconds);
				lastError = new LyricsServiceException(LyricsErrorKind.Json, LocalizationService.TranslateCurrent("LRCLIB communication failed."), ex);
				SetServerBackoff(GetClientRetryDelay(attempt));
			}
			catch (HttpRequestException ex)
			{
				await LogHttpExceptionAsync(requestUrl, statusCode, contentType, responsePrefix, ex, attempt + 1, requestTimer.ElapsedMilliseconds);
				lastError = new LyricsServiceException(LyricsErrorKind.Network, LocalizationService.TranslateCurrent("LRCLIB communication failed."), ex);
				SetServerBackoff(GetClientRetryDelay(attempt));
			}
			if (attempt + 1 < attempts)
			{
				await WaitForServerBackoffAsync(cancellationToken);
			}
		}
		throw lastError ?? new LyricsServiceException(LyricsErrorKind.Network, LocalizationService.TranslateCurrent("LRCLIB communication failed."));
	}

	private async Task LogHttpExceptionAsync(string requestUrl, int? statusCode, string contentType, string responsePrefix, Exception exception, int attempt, long elapsedMs)
	{
		await _logger.WriteAsync(
			$"http-exception method=GET url={requestUrl} status={(statusCode?.ToString(CultureInfo.InvariantCulture) ?? "unavailable")} contentType={Safe(contentType)} attempt={attempt} elapsedMs={elapsedMs} bodyPrefix={responsePrefix} exceptionType={exception.GetType().FullName} exceptionMessage={Safe(exception.Message)} stackTrace={Safe(exception.StackTrace)}");
	}

	private static bool IsTransientStatus(HttpStatusCode statusCode)
	{
		if (statusCode != HttpStatusCode.RequestTimeout && statusCode != HttpStatusCode.TooManyRequests)
		{
			return statusCode >= HttpStatusCode.InternalServerError;
		}
		return true;
	}

	private bool TryReadRequestCache<T>(string relativeUrl, out T? value)
	{
		value = default(T);
		if (!_requestCache.TryGetValue(relativeUrl, out CachedJsonResponse value2))
		{
			return false;
		}
		CachedJsonResponse value3;
		if (value2.ExpiresAtUtc <= DateTimeOffset.UtcNow)
		{
			_requestCache.TryRemove(relativeUrl, out value3);
			return false;
		}
		try
		{
			value = JsonSerializer.Deserialize<T>(value2.Json, _jsonOptions);
			return true;
		}
		catch (JsonException)
		{
			_requestCache.TryRemove(relativeUrl, out value3);
			return false;
		}
	}

	private async Task WaitForServerBackoffAsync(CancellationToken cancellationToken)
	{
		TimeSpan timeSpan;
		lock (_serverBackoffLock)
		{
			timeSpan = _serverBackoffUntilUtc - DateTimeOffset.UtcNow;
		}
		if (timeSpan > TimeSpan.Zero)
		{
			await Task.Delay(timeSpan, cancellationToken);
		}
	}

	private void SetServerBackoff(TimeSpan delay)
	{
		DateTimeOffset dateTimeOffset = DateTimeOffset.UtcNow + delay;
		lock (_serverBackoffLock)
		{
			if (dateTimeOffset > _serverBackoffUntilUtc)
			{
				_serverBackoffUntilUtc = dateTimeOffset;
			}
		}
	}

	private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
	{
		TimeSpan fallback = GetClientRetryDelay(attempt);
		return TimeSpan.FromMilliseconds(Math.Clamp((response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? fallback).TotalMilliseconds, 500.0, 30000.0));
	}

	private static TimeSpan GetClientRetryDelay(int attempt)
	{
		double exponential = 750.0 * Math.Pow(2.0, Math.Clamp(attempt, 0, 4));
		return TimeSpan.FromMilliseconds(Math.Min(12000.0, exponential + Random.Shared.Next(100, 451)));
	}

	private void TrimRequestCache()
	{
		if (_requestCache.Count <= 128)
		{
			return;
		}
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		foreach (KeyValuePair<string, CachedJsonResponse> item in _requestCache)
		{
			if (item.Value.ExpiresAtUtc <= utcNow)
			{
				_requestCache.TryRemove(item.Key, out CachedJsonResponse _);
			}
		}
	}

	private static string Safe(string? value)
	{
		return (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
	}

	private static string BodyPrefix(string? value)
	{
		string body = value ?? string.Empty;
		if (body.Length > 500) body = body.Substring(0, 500);
		return Safe(body);
	}

}
