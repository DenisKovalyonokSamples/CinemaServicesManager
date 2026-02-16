#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.


FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src
COPY CNM.Application/CNM.Application.csproj CNM.Application/
COPY CNM.Domain/CNM.Domain.csproj CNM.Domain/
RUN dotnet restore CNM.Application/CNM.Application.csproj
COPY CNM.Application/. CNM.Application/
COPY CNM.Domain/. CNM.Domain/
WORKDIR /src/CNM.Application
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:3.1 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CNM.Application.dll"]
