# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT_PATH=ContentAggregator.API/ContentAggregator.API.csproj
WORKDIR /src
# Copy project files and restore dependencies
COPY ["ContentAggregator.sln", "./"]
COPY ["ContentAggregator.API/ContentAggregator.API.csproj", "ContentAggregator.API/"]
COPY ["ContentAggregator.Worker/ContentAggregator.Worker.csproj", "ContentAggregator.Worker/"]
COPY ["ContentAggregator.Application/ContentAggregator.Application.csproj", "ContentAggregator.Application/"]
COPY ["ContentAggregator.Core/ContentAggregator.Core.csproj", "ContentAggregator.Core/"]
COPY ["ContentAggregator.Infrastructure/ContentAggregator.Infrastructure.csproj", "ContentAggregator.Infrastructure/"]
RUN dotnet restore "ContentAggregator.sln"
# Copy all source code and build
COPY . .
RUN dotnet build "ContentAggregator.sln" -c Release --no-restore

#FROM build AS publish
RUN dotnet publish "$PROJECT_PATH" -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG APP_DLL=ContentAggregator.API.dll
WORKDIR /app

# Install ffmpeg + yt-dlp dependencies
RUN apt-get update && apt-get install -y ffmpeg python3 python3-pip && \
    pip3 install --no-cache-dir yt-dlp

# Copy published files from build stage
COPY --from=build /app/publish .

# Configure environment and expose port
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80;https://+:443
ENV APP_DLL=${APP_DLL}
EXPOSE 80
EXPOSE 443

# Define entry point
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\" \"$@\"", "--"]
