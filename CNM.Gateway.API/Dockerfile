# Build stage
FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src

COPY CNM.Gateway.API/CNM.Gateway.API.csproj CNM.Gateway.API/
COPY CNM.Showtimes.API/CNM.Showtimes.API.csproj CNM.Showtimes.API/
COPY CNM.Movies.API/CNM.Movies.API.csproj CNM.Movies.API/
COPY CNM.Application/CNM.Application.csproj CNM.Application/
RUN dotnet restore CNM.Gateway.API/CNM.Gateway.API.csproj

COPY . .
RUN dotnet publish CNM.Gateway.API/CNM.Gateway.API.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:3.1 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Development
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CNM.Gateway.API.dll"]
