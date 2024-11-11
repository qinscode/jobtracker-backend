pipeline {
    agent any
    
    environment {
        DOTNET_ENVIRONMENT = 'Production'
        DOCKER_IMAGE_NAME = 'job-tracker-api'
        DOCKER_CONTAINER_NAME = 'job-tracker-api-container'
        DOCKER_IMAGE_TAG = "${BUILD_NUMBER}"
        
        // 数据库配置
        DB_HOST = credentials('DB_HOST')
        DB_USERNAME = credentials('DB_USERNAME')
        DB_CREDS = credentials('DB_PASSWORD')
        DB_DATABASE = credentials('DB_DATABASE')
        
        // API配置
        API_PORT = credentials('API_PORT')
        
        // JWT配置
        JWT_SECRET = credentials('jwt-secret')
        JWT_ISSUER = credentials('JWT_ISSUER')
        JWT_AUDIENCE = credentials('JWT_AUDIENCE')
        
        // Google OAuth配置
        GOOGLE_CLIENT_ID = credentials('Authentication_Google_ClientId')
        GOOGLE_SECRET = credentials('Authentication_Google_ClientSecret')
        
        // Gemini API配置
        GEMINI_API_KEY = credentials('GEMINI_API_KEY')
        GEMINI_API_ENDPOINT = credentials('GEMINI_API_ENDPOINT')
        
        // 添加构建超时设置
        BUILD_TIMEOUT = '30'
    }
    
    options {
        timeout(time: "${env.BUILD_TIMEOUT}" as Integer, unit: 'MINUTES')
        disableConcurrentBuilds()
        buildDiscarder(logRotator(numToKeepStr: '10'))
        ansiColor('xterm')
    }
    
    stages {
        stage('Environment Validation') {
            steps {
                script {
                    // 验证必要的环境变量和凭据
                    def requiredCredentials = [
                        'DB_HOST', 'DB_USERNAME', 'DB_CREDS', 'DB_DATABASE',
                        'API_PORT', 'jwt-secret', 'JWT_ISSUER', 'JWT_AUDIENCE',
                        'Authentication_Google_ClientId', 'Authentication_Google_ClientSecret',
                        'GEMINI_API_KEY', 'GEMINI_API_ENDPOINT'
                    ]
                    
                    requiredCredentials.each { credential ->
                        if (!env.getProperty(credential)) {
                            error "Missing required credential: ${credential}"
                        }
                    }
                }
            }
        }
        
        stage('Configuration Setup') {
            steps {
                script {
                    // 添加错误处理
                    try {
                        def configTemplate = readFile 'appsettings.json'
                        def configContent = configTemplate
                            .replace('#{DB_HOST}', env.DB_HOST)
                            .replace('#{DB_USERNAME}', env.DB_USERNAME)
                            .replace('#{DB_PASSWORD}', env.DB_CREDS)
                            .replace('#{JWT_SECRET}', env.JWT_SECRET)
                            .replace('#{JWT_ISSUER}', env.JWT_ISSUER)
                            .replace('#{JWT_AUDIENCE}', env.JWT_AUDIENCE)
                            .replace('#{GOOGLE_CLIENT_ID}', env.GOOGLE_CLIENT_ID)
                            .replace('#{GOOGLE_SECRET}', env.GOOGLE_SECRET)
                            .replace('#{GEMINI_API_KEY}', env.GEMINI_API_KEY)
                            .replace('#{GEMINI_API_ENDPOINT}', env.GEMINI_API_ENDPOINT)
                            .replace('#{DB_DATABASE}', env.DB_DATABASE)
                        
                        writeFile file: 'appsettings.Production.json', text: configContent
                    } catch (Exception e) {
                        error "配置文件处理失败: ${e.message}"
                    }
                }
            }
        }
        
        stage('Build Docker Image') {
            steps {
                script {
                    try {
                        // 添加构建参数以提高构建性能
                        sh """
                            DOCKER_BUILDKIT=1 docker build \
                                --build-arg BUILDKIT_INLINE_CACHE=1 \
                                --cache-from ${DOCKER_IMAGE_NAME}:latest \
                                -t ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG} .
                            
                            docker tag ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG} ${DOCKER_IMAGE_NAME}:latest
                        """
                    } catch (Exception e) {
                        error "Docker 构建失败: ${e.message}"
                    }
                }
            }
        }
        
        stage('Container Health Check') {
            steps {
                script {
                    def maxRetries = 5
                    def retryInterval = 3
                    
                    // 停止并删除旧容器
                    sh """
                        if docker ps -a | grep -q ${DOCKER_CONTAINER_NAME}; then
                            docker stop ${DOCKER_CONTAINER_NAME} || true
                            docker rm ${DOCKER_CONTAINER_NAME} || true
                        fi
                    """
                    
                    // 启动新容器
                    sh """
                        docker run -d \
                            --name ${DOCKER_CONTAINER_NAME} \
                            --network host \
                            --restart unless-stopped \
                            --health-cmd="curl -f http://localhost:${API_PORT}/health || exit 1" \
                            --health-interval=10s \
                            --health-timeout=5s \
                            --health-retries=3 \
                            -e ASPNETCORE_ENVIRONMENT=Production \
                            ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}
                    """
                    
                    // 等待容器健康检查
                    def healthy = false
                    for (int i = 0; i < maxRetries; i++) {
                        def status = sh(script: "docker inspect --format='{{.State.Health.Status}}' ${DOCKER_CONTAINER_NAME}", returnStdout: true).trim()
                        if (status == 'healthy') {
                            healthy = true
                            break
                        }
                        sleep retryInterval
                    }
                    
                    if (!healthy) {
                        sh "docker logs ${DOCKER_CONTAINER_NAME}"
                        error "Container health check failed after ${maxRetries} attempts"
                    }
                }
            }
        }
        
        stage('Clean Up') {
            steps {
                script {
                    try {
                        // 保留最近3个版本的镜像，添加错误处理
                        sh """
                            docker images ${DOCKER_IMAGE_NAME} --format '{{.ID}} {{.CreatedAt}}' | \
                            sort -k2 -r | \
                            awk 'NR>3 {print \$1}' | \
                            xargs -r docker rmi -f || true
                        """
                    } catch (Exception e) {
                        echo "清理过程中出现警告: ${e.message}"
                    }
                }
            }
        }
    }
    
    post {
        always {
            script {
                // 清理敏感文件和构建产物
                sh '''
                    rm -f appsettings.Production.json
                    docker system prune -f
                '''
                cleanWs(cleanWhenNotBuilt: true,
                       deleteDirs: true,
                       disableDeferredWipeout: true,
                       patterns: [[pattern: 'appsettings.Production.json', type: 'INCLUDE']])
            }
        }
        success {
                    script {
                        // 获取容器状态信息
                        def containerInfo = sh(script: """
                            echo "容器状态: \$(docker ps -f name=${DOCKER_CONTAINER_NAME} --format '{{.Status}}')"
                            echo "运行端口: \$(docker port ${DOCKER_CONTAINER_NAME})"
                        """, returnStdout: true).trim()
                        
                        def deploymentInfo = """
                            Job Tracker API 部署成功!
                            容器名称: ${DOCKER_CONTAINER_NAME}
                            镜像版本: ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}
                            ${containerInfo}
                        """.stripIndent()
                        
                        echo deploymentInfo
                        
                        // 发送成功通知邮件
                        emailext (
                            subject: "构建成功: ${env.JOB_NAME} #${env.BUILD_NUMBER}",
                            body: """
                                构建成功!
                                
                                详情:
                                - 项目: ${env.JOB_NAME}
                                - 构建号: ${env.BUILD_NUMBER}
                                - 容器名称: ${DOCKER_CONTAINER_NAME}
                                - 镜像版本: ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}
                                
                                部署信息:
                                ${containerInfo}
                                
                                构建时间: ${new Date().format("yyyy-MM-dd HH:mm:ss")}
                                持续时间: ${currentBuild.durationString}
                                
                                查看构建日志: ${env.BUILD_URL}console
                            """,
                            to: '${DEFAULT_RECIPIENTS}',
                            mimeType: 'text/html',
                            attachLog: true
                        )
                    }
                }
                
                failure {
                    script {
                        echo '部署失败，正在收集诊断信息...'
                        
                        // 收集诊断信息
                        def diagnosticInfo = ""
                        try {
                            diagnosticInfo = sh(script: """
                                echo "=== Docker 容器日志 ==="
                                docker logs ${DOCKER_CONTAINER_NAME} 2>&1 || echo '无法获取容器日志'
                                
                                echo -e "\n=== 容器状态 ==="
                                docker inspect ${DOCKER_CONTAINER_NAME} 2>&1 || echo '无法获取容器状态'
                                
                                echo -e "\n=== 系统资源状态 ==="
                                df -h
                                free -m
                                docker system df
                            """, returnStdout: true).trim()
                        } catch (Exception e) {
                            diagnosticInfo = "收集诊断信息时出错: ${e.message}"
                        }
                        
                        // 发送失败通知邮件
                        emailext (
                            subject: "构建失败: ${env.JOB_NAME} #${env.BUILD_NUMBER}",
                            body: """
                                构建失败!
                                
                                详情:
                                - 项目: ${env.JOB_NAME}
                                - 构建号: ${env.BUILD_NUMBER}
                                - 失败阶段: ${currentBuild.currentResult}
                                
                                构建信息:
                                - 开始时间: ${new Date(currentBuild.startTimeInMillis).format("yyyy-MM-dd HH:mm:ss")}
                                - 持续时间: ${currentBuild.durationString}
                                
                                诊断信息:
                                <pre>
                                ${diagnosticInfo}
                                </pre>
                                
                                查看完整构建日志: ${env.BUILD_URL}console
                                
                                请尽快检查失败原因并处理!
                            """,
                            to: '${DEFAULT_RECIPIENTS}',
                            mimeType: 'text/html',
                            attachLog: true
                        )
                    }
                }
    }
}