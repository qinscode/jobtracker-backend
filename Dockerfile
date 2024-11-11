# 使用 .NET 7 ASP.NET 运行时作为基础镜像
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

# 使用 .NET 7 SDK 作为构建环境
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# 首先复制项目文件以利用层缓存
COPY ["JobTracker.csproj", "./"]
RUN dotnet restore "JobTracker.csproj"

# 复制其余源代码
COPY . .
RUN dotnet build "JobTracker.csproj" -c Release -o /app/build

# 发布应用
FROM build AS publish
RUN dotnet publish "JobTracker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 最终运行时镜像
FROM base AS final
WORKDIR /app

# 创建非 root 用户
RUN adduser --disabled-password --gecos "" appuser && \
    chown -R appuser:appuser /app

# 定义运行时环境变量
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:80

# 复制发布的应用
COPY --from=publish --chown=appuser:appuser /app/publish .

# 切换到非 root 用户
USER appuser

ENTRYPOINT ["dotnet", "JobTracker.dll"]