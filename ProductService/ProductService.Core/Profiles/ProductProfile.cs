using AutoMapper;
using ProductService.Core.Entities;
using ProductService.Shared.DTO;

namespace ProductService.Core.Profiles;

public class ProductProfile : Profile
{
    public ProductProfile()
    {
        CreateMap<CreateProductDto, Product>()
            .ForMember(dest => dest.Id,      opt => opt.Ignore())
            .ForMember(dest => dest.ImageUrls, opt => opt.Ignore())
            .ForMember(dest => dest.ProductCategories, opt => opt.Ignore())
            // .ForMember(dest => dest.DesignData, opt => opt.Ignore())
            // .ForMember(dest => dest.PreviewImage, opt => opt.Ignore())
            // .ForMember(dest => dest.Status, opt => opt.Ignore())
            // .ForMember(dest => dest.UserId, opt => opt.Ignore())
            // ← маппим ShopId прямо из DTO
            .ForMember(dest => dest.ShopId, opt => opt.MapFrom(src => src.ShopId));

        CreateMap<Product, GetProductDto>()
            .ForCtorParam("Id", opt => opt.MapFrom(src => src.Id.ToString()))
            .ForCtorParam("Name", opt => opt.MapFrom(src => src.Name ?? ""))
            .ForCtorParam("Description", opt => opt.MapFrom(src => src.Description ?? ""))
            .ForCtorParam("Price", opt => opt.MapFrom(src => src.Price))
            .ForCtorParam("StockQuantity", opt => opt.MapFrom(src => src.StockQuantity))
            .ForCtorParam("ImageUrls", opt => opt.MapFrom(src => src.ImageUrls))
            .ForCtorParam("Categories", opt => opt.MapFrom(src =>
                src.ProductCategories
                    .Select(pc => new CategoryNameDto(pc.Category.Id.ToString(), pc.Category.Name))
                    .ToList()
            ));
            // .ForCtorParam("DesignData", opt => opt.MapFrom(src => src.DesignData))
            // .ForCtorParam("PreviewImage", opt => opt.MapFrom(src => src.PreviewImage))
            // .ForCtorParam("Status", opt => opt.MapFrom(src => src.Status))
            // .ForCtorParam("UserId", opt => opt.MapFrom(src => src.UserId));
            
        CreateMap<UpdateProductDto, Product>();
    }
}