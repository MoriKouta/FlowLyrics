using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FlowLyrics.Services;

public sealed class AppLogger
{
	private readonly string _path;

	private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

	public AppLogger(string appDataDirectory)
	{
		string text = Path.Combine(appDataDirectory, "logs");
		Directory.CreateDirectory(text);
		_path = Path.Combine(text, "flowlyrics.log");
	}

	public async Task WriteAsync(string message)
	{
		await WriteBatchAsync(new string[1] { message });
	}

	public async Task WriteBatchAsync(IEnumerable<string> messages)
	{
		string[] batch = messages.Where((string message) => !string.IsNullOrWhiteSpace(message)).ToArray();
		if (batch.Length == 0)
		{
			return;
		}
		await _lock.WaitAsync();
		try
		{
			DateTimeOffset now = DateTimeOffset.Now;
			StringBuilder stringBuilder = new StringBuilder();
			string[] array = batch;
			foreach (string value in array)
			{
				stringBuilder.Append(now.ToString("O")).Append(' ').Append(value)
					.AppendLine();
			}
			await File.AppendAllTextAsync(_path, stringBuilder.ToString(), Encoding.UTF8);
		}
		catch
		{
		}
		finally
		{
			_lock.Release();
		}
	}
}
