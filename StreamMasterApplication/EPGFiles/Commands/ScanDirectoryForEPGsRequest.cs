﻿using AutoMapper;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

using StreamMasterDomain.Dto;

namespace StreamMasterApplication.EPGFiles.Commands;

public class ScanDirectoryForEPGFilesRequest : IRequest<bool>
{
}

public class ScanDirectoryForEPGFilesRequestHandler : IRequestHandler<ScanDirectoryForEPGFilesRequest, bool>
{
    private readonly IAppDbContext _context;
    private readonly IMapper _mapper;
    private readonly IMemoryCache _memoryCache;
    private readonly IPublisher _publisher;

    public ScanDirectoryForEPGFilesRequestHandler(
         IPublisher publisher,
          IMemoryCache memoryCache,
          IMapper mapper,
         IAppDbContext context)
    {
        _publisher = publisher;
        _mapper = mapper;
        _memoryCache = memoryCache;
        _context = context;
    }

    public async Task<bool> Handle(ScanDirectoryForEPGFilesRequest command, CancellationToken cancellationToken)
    {
        var fd = FileDefinitions.EPG;

        DirectoryInfo EPGDirInfo = new(fd.DirectoryLocation);

        EnumerationOptions er = new()
        {
            MatchCasing = MatchCasing.CaseInsensitive
        };

        var files = EPGDirInfo.GetFiles("*.*", SearchOption.AllDirectories)
           .Where(s => s.FullName.ToLower().EndsWith(fd.FileExtension.ToLower()) || s.FullName.ToLower().EndsWith(fd.FileExtension + ".gz".ToLower()));

        foreach (FileInfo EPGFileInfo in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (EPGFileInfo.DirectoryName == null)
            {
                continue;
            }

            EPGFile? epgFile = await _context.EPGFiles.FirstOrDefaultAsync(a => a.Source.ToLower().Equals(EPGFileInfo.Name.ToLower()), cancellationToken: cancellationToken).ConfigureAwait(false);

            if (epgFile == null)
            {
                string Url = "";
                string filePath = Path.Combine(EPGFileInfo.DirectoryName, Path.GetFileNameWithoutExtension(EPGFileInfo.FullName) + ".url");

                if (FileUtil.ReadUrlFromFile(filePath, out string? url))
                {
                    Url = url;
                }

                epgFile = new EPGFile
                {
                    Name = Path.GetFileNameWithoutExtension(EPGFileInfo.Name),
                    Source = EPGFileInfo.Name,
                    Description = $"Imported from {EPGFileInfo.Name}",
                    LastDownloaded = EPGFileInfo.LastWriteTime,
                    LastDownloadAttempt = DateTime.Now,
                    FileExists = true,
                    Url = Url
                };

                epgFile.SetFileDefinition(FileDefinitions.EPG);

                _ = _context.EPGFiles.Add(epgFile);
                _ = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            if (string.IsNullOrEmpty(epgFile.Url))
            {
                epgFile.LastDownloaded = EPGFileInfo.LastWriteTime;
                _ = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            EPGFilesDto ret = _mapper.Map<EPGFilesDto>(epgFile);
            await _publisher.Publish(new EPGFileAddedEvent(ret), cancellationToken).ConfigureAwait(false);
        }

        return true;
    }
}
