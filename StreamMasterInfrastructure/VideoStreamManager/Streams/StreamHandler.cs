﻿using Microsoft.Extensions.Logging;

using StreamMasterApplication.Common.Interfaces;

using System.Collections.Concurrent;
using System.Diagnostics;

namespace StreamMasterInfrastructure.VideoStreamManager.Streams;

/// <summary>
/// Manages the streaming of a single video stream, including client registrations and circularRingbuffer handling.
/// </summary>
public class StreamHandler(
    string streamURL,
    string videoStreamId,
    string videoStreamName,
    int processId,
    ILogger<IStreamHandler> logger,
    ICircularRingBuffer ringBuffer,
    IClientStreamerManager clientStreamerManager,
    CancellationTokenSource cancellationTokenSource
    ) : IStreamHandler
{
    private readonly ConcurrentDictionary<Guid, Guid> clientStreamerIds = new();

    public bool FailoverInProgress { get; set; }
    public int M3UFileId { get; set; }
    public int ProcessId { get; set; } = processId;
    public ICircularRingBuffer RingBuffer { get; } = ringBuffer;
    public string StreamUrl { get; } = streamURL;
    public string VideoStreamId { get; } = videoStreamId;
    public string VideoStreamName { get; } = videoStreamName;

    private async Task DelayWithCancellation(int milliseconds)
    {
        try
        {
            await Task.Delay(milliseconds, VideoStreamingCancellationToken.Token);
        }
        catch (TaskCanceledException)
        {
            logger.LogInformation("Task was cancelled");
            throw;
        }
    }

    private async Task LogRetryAndDelay(int retryCount, int maxRetries, int waitTime)
    {
        if (VideoStreamingCancellationToken.Token.IsCancellationRequested)
        {
            logger.LogInformation("Stream was cancelled for: {StreamUrl} {name}", StreamUrl, VideoStreamName);
        }

        logger.LogInformation("Stream received 0 bytes for stream: {StreamUrl} Retry {retryCount}/{maxRetries} {name}",
            StreamUrl,
            retryCount,
            maxRetries, VideoStreamName);

        await DelayWithCancellation(waitTime);
    }

    public async Task StartVideoStreamingAsync(Stream stream, ICircularRingBuffer circularRingbuffer)
    {
        const int chunkSize = 24 * 1024;

        logger.LogInformation("Starting video read streaming, chunk size is {ChunkSize}, for stream: {StreamUrl} {name}", chunkSize, StreamUrl, VideoStreamName);

        Memory<byte> bufferMemory = new byte[chunkSize];

        const int maxRetries = 3; //setting.MaxConnectRetry > 0 ? setting.MaxConnectRetry : 3;
        const int waitTime = 50;// setting.MaxConnectRetryTimeMS > 0 ? setting.MaxConnectRetryTimeMS : 50;

        using (stream)
        {
            int retryCount = 0;
            while (!VideoStreamingCancellationToken.IsCancellationRequested && retryCount < maxRetries)
            {
                try
                {
                    int bytesRead = await TryReadStream(bufferMemory, stream);
                    if (bytesRead == -1)
                    {
                        break;
                    }
                    if (bytesRead == 0)
                    {
                        retryCount++;
                        await LogRetryAndDelay(retryCount, maxRetries, waitTime);
                    }
                    else
                    {
                        circularRingbuffer.WriteChunk(bufferMemory[..bytesRead]);
                        retryCount = 0;
                    }
                }
                catch (TaskCanceledException ex)
                {
                    logger.LogError(ex, "Stream cancelled for: {StreamUrl} {name}", StreamUrl, VideoStreamName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Stream error for: {StreamUrl} {name}", StreamUrl, VideoStreamName);
                    break;
                }
            }
        }

        logger.LogInformation("Stream stopped for: {StreamUrl} {name}", StreamUrl, VideoStreamName);
        if (!VideoStreamingCancellationToken.IsCancellationRequested)
        {
            VideoStreamingCancellationToken.Cancel();
        }
    }

    private async Task<int> TryReadStream(Memory<byte> bufferChunk, Stream stream)
    {
        try
        {
            if (!stream.CanRead || VideoStreamingCancellationToken.Token.IsCancellationRequested)
            {
                logger.LogWarning("Stream is not readable or cancelled for: {StreamUrl} {name}", StreamUrl, VideoStreamName);
                return -1;
            }

            return await stream.ReadAsync(bufferChunk, VideoStreamingCancellationToken.Token);
        }
        catch (TaskCanceledException)
        {
            return -1;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public CancellationTokenSource VideoStreamingCancellationToken { get; set; } = cancellationTokenSource;

    public int ClientCount => clientStreamerIds.Count;

    public bool IsFailed { get; private set; }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
    public async Task RegisterClientStreamer(Guid ClientId, CancellationToken cancellationToken = default)
    {
        IClientStreamerConfiguration? streamerConfiguration = await clientStreamerManager.GetClientStreamerConfiguration(ClientId, cancellationToken);
        if (streamerConfiguration == null)
        {
            logger.LogError("Error registering stream configuration for client {ClientId} {name}, streamerConfiguration null.", ClientId, VideoStreamName);
            return;
        }
        try
        {
            clientStreamerIds.TryAdd(streamerConfiguration.ClientId, streamerConfiguration.ClientId);
            RingBuffer.RegisterClient(streamerConfiguration.ClientId, streamerConfiguration.ClientUserAgent, streamerConfiguration.ClientIPAddress);
            await clientStreamerManager.SetClientBufferDelegate(streamerConfiguration.ClientId, RingBuffer, cancellationToken);

            logger.LogInformation("RegisterClientStreamer for Client ID {ClientId} to Video Stream Id {videoStreamId} {name}", streamerConfiguration.ClientId, VideoStreamId, VideoStreamName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error registering stream configuration for client {ClientId} {name}.", streamerConfiguration.ClientId, VideoStreamName);
        }
    }

    protected virtual void OnStreamControllerStopped()
    {
        logger.LogWarning("Stopping stream of {VideoStreamId}", VideoStreamId);
    }

    public void Stop()
    {
        if (VideoStreamingCancellationToken?.IsCancellationRequested == false)
        {
            VideoStreamingCancellationToken.Cancel();
        }

        if (ProcessId > 0)
        {
            try
            {
                string? procName = CheckProcessExists();
                if (procName != null)
                {
                    Process process = Process.GetProcessById(ProcessId);
                    process.Kill();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error killing process {ProcessId}.", ProcessId);
            }
        }
        OnStreamControllerStopped();
    }

    public bool UnRegisterClientStreamer(Guid ClientId)
    {
        try
        {
            logger.LogInformation("UnRegisterClientStreamer ClientId: {ClientId} {name}", ClientId, VideoStreamName);
            bool result = clientStreamerIds.TryRemove(ClientId, out _);
            RingBuffer.UnRegisterClient(ClientId);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unregistering stream configuration for client {ClientId} {name}", ClientId, VideoStreamName);
            return false;
        }
    }

    private string? CheckProcessExists()
    {
        try
        {
            Process process = Process.GetProcessById(ProcessId);
            // logger.LogInformation($"Process with ID {processId} exists. Name: {process.ProcessName}");
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            // logger.LogWarning($"Process with ID {processId} does not exist.");
            return null;
        }
    }

    public ICollection<IClientStreamerConfiguration>? GetClientStreamerConfigurations()
    {
        return clientStreamerManager.GetClientStreamerConfigurationsByChannelVideoStreamId(VideoStreamId);
    }

    public IEnumerable<Guid> GetClientStreamerClientIds()
    {
        return clientStreamerIds.Keys;
    }

    public bool HasClient(Guid clientId)
    {
        return clientStreamerManager.HasClient(VideoStreamId, clientId);
    }

    public void SetFailed()
    {
        IsFailed = true;
    }
}