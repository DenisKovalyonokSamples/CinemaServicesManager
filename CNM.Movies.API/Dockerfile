# Build stage
FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src

COPY CNM.Movies.API/CNM.Movies.API.csproj CNM.Movies.API/
COPY CNM.Showtimes.API/CNM.Showtimes.API.csproj CNM.Showtimes.API/
COPY CNM.Gateway.API/CNM.Gateway.API.csproj CNM.Gateway.API/
COPY CNM.Application/CNM.Application.csproj CNM.Application/
RUN dotnet restore CNM.Movies.API/CNM.Movies.API.csproj

COPY . .
RUN dotnet publish CNM.Movies.API/CNM.Movies.API.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:3.1 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Development
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CNM.Movies.API.dll"]
