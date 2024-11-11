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
        DB_PORT = credentials('DB_PORT')
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
    }
    
    stages {
        stage('Configuration Setup') {
            steps {
                script {
                    def configTemplate = readFile 'appsettings.json'
                    def configContent = configTemplate
                        .replace('#{DB_HOST}', env.DB_HOST)
                        .replace('#{DB_USERNAME}', env.DB_USERNAME)
                        .replace('#{DB_PASSWORD}', env.DB_CREDS)
                        .replace('#{DB_PORT}', env.DB_PORT)
                        .replace('#{JWT_SECRET}', env.JWT_SECRET)
                        .replace('#{JWT_ISSUER}', env.JWT_ISSUER)
                        .replace('#{JWT_AUDIENCE}', env.JWT_AUDIENCE)
                        .replace('#{GOOGLE_CLIENT_ID}', env.GOOGLE_CLIENT_ID)
                        .replace('#{GOOGLE_SECRET}', env.GOOGLE_SECRET)
                        .replace('#{GEMINI_API_KEY}', env.GEMINI_API_KEY)
                        .replace('#{GEMINI_API_ENDPOINT}', env.GEMINI_API_ENDPOINT)
                        .replace('#{DB_DATABASE}', env.DB_DATABASE)
                    
                    writeFile file: 'appsettings.Production.json', text: configContent
                }
            }
        }
        
        stage('Build Docker Image') {
            steps {
                script {
                    // 直接使用项目中的Dockerfile构建镜像
                    sh """
                        docker build -t ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG} .
                        docker tag ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG} ${DOCKER_IMAGE_NAME}:latest
                    """
                }
            }
        }
        
        stage('Deploy') {
            steps {
                script {
                    // 停止并删除旧容器（如果存在）
                    sh """
                        if docker ps -a | grep -q ${DOCKER_CONTAINER_NAME}; then
                            docker stop ${DOCKER_CONTAINER_NAME} || true
                            docker rm ${DOCKER_CONTAINER_NAME} || true
                        fi
                    """
                    
                    // 启动新容器，只设置必要的环境变量
                    sh """
                        docker run -d \
                            --name ${DOCKER_CONTAINER_NAME} \
                            --network host \
                            --restart unless-stopped \
                            -e ASPNETCORE_ENVIRONMENT=Production \
                            ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}
                    """
                    
                    // 验证容器是否成功启动
                    sh """
                        sleep 10
                        if ! docker ps | grep -q ${DOCKER_CONTAINER_NAME}; then
                            echo "Container failed to start"
                            docker logs ${DOCKER_CONTAINER_NAME}
                            exit 1
                        fi
                        
                        echo "Container started successfully"
                        docker logs ${DOCKER_CONTAINER_NAME}
                    """
                }
            }
        }
        
        stage('Clean Up') {
            steps {
                script {
                    // 保留最近3个版本的镜像
                    sh """
                        if [ \$(docker images ${DOCKER_IMAGE_NAME} -q | wc -l) -gt 3 ]; then
                            docker images ${DOCKER_IMAGE_NAME} -q | tail -n +4 | xargs docker rmi -f || true
                        fi
                    """
                }
            }
        }
    }
    
    post {
        always {
            // 清理敏感文件和构建产物
            sh '''
                rm -f appsettings.Production.json
            '''
            cleanWs()
        }
        success {
            echo 'Job Tracker API 部署成功!'
            echo "容器名称: ${DOCKER_CONTAINER_NAME}"
            echo "镜像版本: ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"
        }
        failure {
            echo '部署失败，请检查日志!'
            sh "docker logs ${DOCKER_CONTAINER_NAME} || true"
        }
    }
}