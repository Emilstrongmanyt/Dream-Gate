FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY Kindling.sln ./
COPY sim/ ./sim/
COPY tools/MatchHost/ ./tools/MatchHost/
COPY tools/HeadlessAlpha/ ./tools/HeadlessAlpha/
COPY content/ ./content/
RUN dotnet publish tools/MatchHost/MatchHost.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /out ./
COPY content/ ./content/
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MatchHost.dll"]
