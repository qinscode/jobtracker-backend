# 使用 .NET 7 ASP.NET 运行时作为基础镜像
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

# 使用 .NET 7 SDK 作为构建环境
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "JobTracker.csproj"
RUN dotnet build "JobTracker.csproj" -c Release -o /app/build

# 发布应用
FROM build AS publish
RUN dotnet publish "JobTracker.csproj" -c Release -o /app/publish

# 使用运行时镜像，运行发布的应用
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "JobTracker.dll"]

ENV ASPNETCORE_ENVIRONMENT=Production