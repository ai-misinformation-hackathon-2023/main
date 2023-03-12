FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["FactBot/FactBot.csproj", "FactBot/"]
RUN dotnet restore "FactBot/FactBot.csproj"
COPY . .
WORKDIR "/src/FactBot"
RUN dotnet build "FactBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FactBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FactBot.dll"]
