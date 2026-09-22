using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;

namespace FlowLyrics.Tests;

public sealed class LrclibTransportTests : IDisposable
{
	private readonly string _directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-transport-" + Guid.NewGuid().ToString("N"));

	[Fact]
	public async Task CachedResponse_KeepsHeadersAndReturnsIndependentRecords()
	{
		using Handler handler = new((request, _) =>
		{
			Assert.Equal("https://lrclib.net/api/get/123", request.RequestUri!.AbsoluteUri);
			Assert.Contains(request.Headers.UserAgent, value => value.Product?.Name == "FlowLyrics");
			Assert.Contains(request.Headers.Accept, value => value.MediaType == "application/json");
			return Task.FromResult(RecordResponse());
		});
		using LyricsService service = new(_directory, handler);
		var first = await service.GetRecordByIdAsync(123, CancellationToken.None);
		first!.TrackName = "changed by caller";
		var second = await service.GetRecordByIdAsync(123, CancellationToken.None);
		Assert.Equal("Song", second!.TrackName);
		Assert.NotSame(first, second);
		Assert.Equal(1, handler.Requests);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ConcurrentReaders_ShareRequest_AndWaitingCancellationDoesNotCancelOwner(bool cancelWaiter)
	{
		TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
		using Handler handler = new(async (_, token) =>
		{
			started.TrySetResult();
			await release.Task.WaitAsync(token);
			return RecordResponse();
		});
		using LyricsService service = new(_directory, handler);
		using CancellationTokenSource waiterCancellation = new();
		Task<LrclibRecord?> owner = service.GetRecordByIdAsync(123, CancellationToken.None);
		await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
		Task<LrclibRecord?> waiter = service.GetRecordByIdAsync(123, waiterCancellation.Token);
		try
		{
			Assert.False(waiter.IsCompleted);
			if (cancelWaiter)
			{
				waiterCancellation.Cancel();
				await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
			}
		}
		finally { release.TrySetResult(); }
		Assert.Equal(123, (await owner)!.Id);
		if (!cancelWaiter) Assert.Equal(123, (await waiter)!.Id);
		Assert.Equal(123, (await service.GetRecordByIdAsync(123, CancellationToken.None))!.Id);
		Assert.Equal(1, handler.Requests);
	}

	[Fact]
	public async Task ActiveRequestCancellation_Propagates_WithoutRetryOrTimeoutError()
	{
		TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
		using Handler handler = new(async (_, token) =>
		{
			started.TrySetResult();
			await Task.Delay(Timeout.Infinite, token);
			return RecordResponse();
		});
		using LyricsService service = new(_directory, handler);
		using CancellationTokenSource cancellation = new();
		var request = service.GetRecordByIdAsync(123, cancellation.Token);
		await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
		cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
		Assert.Equal(1, handler.Requests);
	}

	[Fact]
	public async Task RateLimit_RetriesAndCachesSuccessfulResponse()
	{
		int attempts = 0;
		using Handler handler = new((_, _) => Task.FromResult(
			Interlocked.Increment(ref attempts) == 1 ? RetryResponse(HttpStatusCode.TooManyRequests) : RecordResponse()));
		using LyricsService service = new(_directory, handler);
		Assert.Equal(123, (await service.GetRecordByIdAsync(123, CancellationToken.None))!.Id);
		await service.GetRecordByIdAsync(123, CancellationToken.None);
		Assert.Equal(2, handler.Requests);
	}

	[Theory]
	[InlineData("rate-limit", LyricsErrorKind.RateLimited)]
	[InlineData("server", LyricsErrorKind.Network)]
	[InlineData("json", LyricsErrorKind.Json)]
	[InlineData("timeout", LyricsErrorKind.Timeout)]
	[InlineData("network", LyricsErrorKind.Network)]
	public async Task ExhaustedRetries_PreserveErrorClassification(string failure, LyricsErrorKind expected)
	{
		using Handler handler = new((_, _) => failure switch
		{
			"rate-limit" => Task.FromResult(RetryResponse(HttpStatusCode.TooManyRequests)),
			"server" => Task.FromResult(RetryResponse(HttpStatusCode.InternalServerError)),
			"json" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("invalid json") }),
			"timeout" => Task.FromException<HttpResponseMessage>(new TaskCanceledException("transport timeout")),
			_ => Task.FromException<HttpResponseMessage>(new HttpRequestException("transport unavailable"))
		});
		using LyricsService service = new(_directory, handler);
		var error = await Assert.ThrowsAsync<LyricsServiceException>(() => service.GetRecordByIdAsync(123, CancellationToken.None));
		Assert.Equal(expected, error.Kind);
		Assert.Equal(2, handler.Requests);
	}

	[Fact]
	public void ServiceDisposal_DisposesOwnedTransport()
	{
		using Handler handler = new((_, _) => Task.FromResult(RecordResponse()));
		LyricsService service = new(_directory, handler);
		service.Dispose();
		Assert.True(handler.Disposed);
	}

	private static HttpResponseMessage RecordResponse() => new(HttpStatusCode.OK)
	{
		Content = new StringContent("{\"id\":123,\"trackName\":\"Song\",\"artistName\":\"Artist\",\"duration\":240}")
	};

	private static HttpResponseMessage RetryResponse(HttpStatusCode status)
	{
		HttpResponseMessage response = new(status);
		response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
		return response;
	}

	private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
	{
		private int _requests;
		public int Requests => Volatile.Read(ref _requests);
		public bool Disposed { get; private set; }
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _requests);
			return send(request, cancellationToken);
		}
		protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
	}

	public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
