using AutoMapper;
using ktl.be.applications.Document.Dtos.SignTemplate;
using ktl.be.domain.Documents;

namespace ktl.be.applications.Base
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<SignTemplate, ViewSignTemplateDto>();
            CreateMap<SignTemplatePosition, SignTemplatePositionDto>();
        }
    }
}
