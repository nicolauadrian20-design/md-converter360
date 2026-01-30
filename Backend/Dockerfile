# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj, nuget config and restore dependencies
COPY MDConverter360.csproj .
COPY NuGet.config .
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install dependencies for QuestPDF and Docnet.Core (PDFium)
RUN apt-get update && apt-get install -y \
    libfontconfig1 \
    libfreetype6 \
    libgdiplus \
    libc6-dev \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser

# Copy published app
COPY --from=build /app/publish .

# Create directories for temp storage
RUN mkdir -p /app/Storage /app/Logs && chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port (Render will use PORT env var)
EXPOSE 10000

# Set environment variables - Render provides PORT at runtime
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check - use PORT env var (defaults to 10000 on Render)
HEALTHCHECK --interval=30s --timeout=30s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:${PORT:-10000}/api/health || exit 1

# Start the application with dynamic port binding
# Render sets PORT env var, we need to bind to it
CMD ASPNETCORE_URLS=http://+:${PORT:-10000} dotnet MDConverter360.dll
