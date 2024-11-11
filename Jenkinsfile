pipeline {
    agent any
    
    options {
        timeout(time: 30, unit: 'MINUTES')  // set timeout for the pipeline
        disableConcurrentBuilds()           // disable concurrent builds
        ansiColor('xterm')                  // enable ANSI color output
    }
    
    environment {
        // 应用配置
        APP_NAME = 'job-tracker-api'
        DOCKER_IMAGE = "${APP_NAME}"
        DOCKER_TAG = "${BUILD_NUMBER}"
        CONTAINER_NAME = "${APP_NAME}-container"
        
        // 凭据配置 - 使用 credentials 绑定
        DATABASE_CREDS = credentials('database-credentials')     
        JWT_CREDS = credentials('jwt-credentials')               
        GOOGLE_CREDS = credentials('google-oauth-credentials')   
        GEMINI_CREDS = credentials('gemini-api-credentials')     
        
        // 运行时配置
        DOTNET_ENVIRONMENT = 'Production'
        MAX_IMAGES_TO_KEEP = 3
    }
    
    stages {
        stage('Prepare') {
            steps {
                script {
                    // 清理工作空间
                    cleanWs()
                    
                    // 检出代码
                    checkout scm
                    
                    // 验证必要工具
                    sh '''
                        docker --version
                        dotnet --version
                    '''
                }
            }
        }
        
        stage('Build Configuration') {
            steps {
                script {
                    // 生成配置文件
                    def config = readJSON file: 'appsettings.json'
                    
                    // 更新配置
                    config.ConnectionStrings.DefaultConnection = "Host=${DATABASE_CREDS_USR};Database=${env.DB_DATABASE};Username=${DATABASE_CREDS_USR};Password=${DATABASE_CREDS_PSW}"
                    config.Jwt.Secret = "${JWT_CREDS_PSW}"
                    config.Jwt.Issuer = "${JWT_CREDS_USR}"
                    config.Authentication.Google.ClientId = "${GOOGLE_CREDS_USR}"
                    config.Authentication.Google.ClientSecret = "${GOOGLE_CREDS_PSW}"
                    config.Gemini.ApiKey = "${GEMINI_CREDS_PSW}"
                    config.Gemini.ApiEndpoint = "${GEMINI_CREDS_USR}"
                    
                    // 保存新配置
                    writeJSON file: 'appsettings.Production.json', json: config
                }
            }
        }
        
        stage('Build & Push Docker Image') {
            steps {
                script {
                    try {
                        // 构建 Docker 镜像
                        docker.build("${DOCKER_IMAGE}:${DOCKER_TAG}", "--no-cache .")
                        
                        // 标记最新版本
                        sh "docker tag ${DOCKER_IMAGE}:${DOCKER_TAG} ${DOCKER_IMAGE}:latest"
                        
                        // 可选：推送到私有仓库
                        // docker.withRegistry('https://your-registry', 'registry-credentials') {
                        //     docker.image("${DOCKER_IMAGE}:${DOCKER_TAG}").push()
                        //     docker.image("${DOCKER_IMAGE}:latest").push()
                        // }
                    } catch (Exception e) {
                        currentBuild.result = 'FAILURE'
                        error "Docker 构建失败: ${e.message}"
                    }
                }
            }
        }
        
        stage('Deploy') {
            steps {
                timeout(time: 5, unit: 'MINUTES') {  // 部署超时设置
                    script {
                        try {
                            // 优雅停止旧容器
                            sh """
                                if docker ps -a | grep -q ${CONTAINER_NAME}; then
                                    echo "正在停止旧容器..."
                                    docker stop ${CONTAINER_NAME} || true
                                    docker rm ${CONTAINER_NAME} || true
                                fi
                            """
                            
                            // 启动新容器
                            sh """
                                docker run -d \
                                    --name ${CONTAINER_NAME} \
                                    --network host \
                                    --restart unless-stopped \
                                    --health-cmd "curl -f http://localhost/health || exit 1" \
                                    --health-interval 30s \
                                    --health-timeout 10s \
                                    --health-retries 3 \
                                    -e ASPNETCORE_ENVIRONMENT=${DOTNET_ENVIRONMENT} \
                                    ${DOCKER_IMAGE}:${DOCKER_TAG}
                            """
                            
                            // 等待容器健康检查
                            sh """
                                echo "等待容器启动..."
                                attempt=1
                                max_attempts=10
                                until docker ps --filter "name=${CONTAINER_NAME}" --filter "health=healthy" | grep -q "${CONTAINER_NAME}" || [ \$attempt -eq \$max_attempts ]; do
                                    echo "等待健康检查通过... (尝试 \$attempt/\$max_attempts)"
                                    sleep 5
                                    attempt=\$((attempt + 1))
                                done
                                
                                if [ \$attempt -eq \$max_attempts ]; then
                                    echo "容器健康检查失败"
                                    docker logs ${CONTAINER_NAME}
                                    exit 1
                                fi
                                
                                echo "容器已成功启动并通过健康检查"
                            """
                        } catch (Exception e) {
                            currentBuild.result = 'FAILURE'
                            error "部署失败: ${e.message}"
                        }
                    }
                }
            }
        }
        
        stage('Cleanup') {
            steps {
                script {
                    try {
                        // 清理旧镜像
                        sh """
                            echo "清理旧镜像..."
                            if [ \$(docker images ${DOCKER_IMAGE} -q | wc -l) -gt ${MAX_IMAGES_TO_KEEP} ]; then
                                docker images ${DOCKER_IMAGE} --format '{{.ID}} {{.CreatedAt}}' | sort -k 2 -r | tail -n +\$((${MAX_IMAGES_TO_KEEP} + 1)) | awk '{print \$1}' | xargs -r docker rmi -f
                            fi
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
                // 清理敏感文件
                sh '''
                    rm -f appsettings.Production.json
                    rm -f *.tmp
                '''
                
                // 清理工作空间
                cleanWs(
                    cleanWhenFailure: true,
                    cleanWhenUnstable: true,
                    deleteDirs: true,
                    patterns: [[pattern: '**/*.json', type: 'INCLUDE']]
                )
            }
        }
        
        success {
            script {
                // 发送成功通知
                emailext (
                    subject: "构建成功: ${env.JOB_NAME} #${env.BUILD_NUMBER}",
                    body: """
                        构建成功!
                        
                        详情:
                        - 项目: ${env.JOB_NAME}
                        - 构建号: ${env.BUILD_NUMBER}
                        - 容器名称: ${CONTAINER_NAME}
                        - 镜像版本: ${DOCKER_IMAGE}:${DOCKER_TAG}
                        
                        查看构建日志: ${env.BUILD_URL}console
                    """,
                    to: '${DEFAULT_RECIPIENTS}'
                )
            }
        }
        
        failure {
            script {
                // 发送失败通知
                emailext (
                    subject: "构建失败: ${env.JOB_NAME} #${env.BUILD_NUMBER}",
                    body: """
                        构建失败!
                        
                        详情:
                        - 项目: ${env.JOB_NAME}
                        - 构建号: ${env.BUILD_NUMBER}
                        
                        失败阶段: ${currentBuild.result}
                        
                        容器日志:
                        ${sh(script: "docker logs ${CONTAINER_NAME} 2>&1 || echo '无法获取容器日志'", returnStdout: true)}
                        
                        查看构建日志: ${env.BUILD_URL}console
                    """,
                    to: '${DEFAULT_RECIPIENTS}'
                )
            }
        }
    }
}