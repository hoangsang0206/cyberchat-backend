FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/CyberChat.API/CyberChat.API.csproj", "src/CyberChat.API/"]
COPY ["src/CyberChat.Infrastructure/CyberChat.Infrastructure.csproj", "src/CyberChat.Infrastructure/"]
COPY ["src/CyberChat.Application/CyberChat.Application.csproj", "src/CyberChat.Application/"]
COPY ["src/CyberChat.Domain/CyberChat.Domain.csproj", "src/CyberChat.Domain/"]
RUN dotnet restore "src/CyberChat.API/CyberChat.API.csproj"
COPY . .
WORKDIR "/src/src/CyberChat.API"
RUN dotnet build "CyberChat.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CyberChat.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CyberChat.API.dll"]
