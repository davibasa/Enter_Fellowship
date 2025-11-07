# See https://aka.ms/customizecontainer to learn how to customize your debug container
# Stage base para runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Stage de build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Enter_Extractor_Api.csproj", "."]
RUN dotnet restore "Enter_Extractor_Api.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "Enter_Extractor_Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Stage de publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Enter_Extractor_Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage final
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Enter_Extractor_Api.dll"]
