namespace ProductService.Shared.DTO.Jwt;

public record RefreshDto(string AccessToken, string RefreshToken);