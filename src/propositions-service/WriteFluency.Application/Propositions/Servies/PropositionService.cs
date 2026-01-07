using System;
using Microsoft.EntityFrameworkCore;
using WriteFluency.Common;
using WriteFluency.Data;

namespace WriteFluency.Propositions;

public class PropositionService
{
    private readonly IAppDbContext _context;
    private readonly IFileService _fileService;

    public PropositionService(IAppDbContext context, IFileService fileService)
    {
        _context = context;
        _fileService = fileService;
    }

    public async Task<Proposition?> GetAsync(int id)
    {
        var proposition = await _context.Propositions.FindAsync(id);
        
        if (proposition is null)
        {
            return null;
        }

        return proposition;
    }

    public async Task<PropositionDto> GetAsync(GetPropositionDto dto)
    {
        var propositionQuery = _context.Propositions
            .Where(p => p.SubjectId == dto.Subject && p.ComplexityId == dto.Complexity);
        
        if(dto.AlreadyGeneratedIds is not null && dto.AlreadyGeneratedIds.Any())
        {
            propositionQuery = propositionQuery.Where(p => !dto.AlreadyGeneratedIds.Contains(p.Id));
        }

        var proposition = await propositionQuery.OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        
        if (proposition is null)
        {
            proposition = await _context.Propositions.FirstAsync();
        }

        var audio = await _fileService.GetFileAsync(Proposition.AudioBucketName, proposition.AudioFileId);

        return new PropositionDto(audio, proposition);
    }
}
