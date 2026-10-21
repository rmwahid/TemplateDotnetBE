# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY TemplateDotnetBE.Functional.slnx .
COPY API/API.csproj API/
COPY Constants/Constants.csproj Constants/
COPY Entities/Entities.csproj Entities/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY Model/Model.csproj Model/
COPY Query/Query.csproj Query/
COPY Repository/Repository.csproj Repository/
COPY Service/Service.csproj Service/
COPY Logger/Logger.csproj Logger/
COPY Migrations.Core/Migrations.Core.csproj Migrations.Core/
COPY Migrations.PostgreSQL/Migrations.PostgreSQL.csproj Migrations.PostgreSQL/
COPY Migrations.MySQL/Migrations.MySQL.csproj Migrations.MySQL/

RUN dotnet restore TemplateDotnetBE.Functional.slnx

# Copy source code
COPY . .
RUN dotnet publish API/API.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "API.dll"]
