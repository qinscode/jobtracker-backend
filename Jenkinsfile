pipeline {
    agent any
    
    environment {
        // 数据库配置
        DB_HOST = credentials('postgres-credentials')
        DB_NAME = credentials('DB_USERNAME')
        DB_CREDS = credentials('DB_PASSWORD')
        DB_PORT = credentials('DB_PORT')
        
        API_PORT = credentials('API_PORT')
        
        // JWT 配置
        JWT_SECRET = credentials('jwt-secret')
        JWT_ISSUER = credentials('JWT_ISSUER')
        JWT_AUDIENCE = credentials('JWT_AUDIENCE')
        
        // Google OAuth 配置
        GOOGLE_CLIENT_ID = credentials('Authentication_Google_ClientId')
        GOOGLE_SECRET = credentials('Authentication_Google_ClientSecret')
        
        // Gemini API 配置
        GEMINI_API_KEY = credentials('GEMINI_API_KEY')
        GEMINI_API_ENDPOINT = credentials('GEMINI_API_ENDPOINT')
    }
    
    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Prepare Configuration') {
            steps {
                script {
                    // 创建临时配置文件
                    sh 'cp appsettings.json appsettings.tmp.json'
                }
            }
        }

        stage('Configure Database') {
            steps {
                script {
                    sh """
                        escapedHost=\$(echo \"${DB_HOST}\" | sed 's/[&/\\]/\\\\&/g')
                        escapedName=\$(echo \"${DB_NAME}\" | sed 's/[&/\\]/\\\\&/g')
                        escapedUser=\$(echo \"${DB_CREDS_USR}\" | sed 's/[&/\\]/\\\\&/g')
                        escapedPass=\$(echo \"${DB_CREDS_PSW}\" | sed 's/[&/\\]/\\\\&/g')
                        escapedPort=\$(echo \"${DB_PORT}\" | sed 's/[&/\\]/\\\\&/g')
                        
                        sed -i "s/#{DB_HOST}/\${escapedHost}/g" appsettings.tmp.json
                        sed -i "s/#{DB_NAME}/\${escapedName}/g" appsettings.tmp.json
                        sed -i "s/#{DB_USERNAME}/\${escapedUser}/g" appsettings.tmp.json
                        sed -i "s/#{DB_PASSWORD}/\${escapedPass}/g" appsettings.tmp.json
                        sed -i "s/#{DB_PORT}/\${escapedPort}/g" appsettings.tmp.json
                    """
                }
            }
        }

        stage('Configure JWT') {
            steps {
                script {
                    sh """
                        escapedSecret=\$(echo \"${JWT_SECRET}\" | sed 's/[&/\\]/\\\\&/g')
                        escapedIssuer=\$(echo \"${JWT_ISSUER}\" | sed 's/[&/\\]/\\\\&/g')
                        escapedAudience=\$(echo \"${JWT_AUDIENCE}\" | sed 's/[&/\\]/\\\\&/g')
                        
                        sed -i "s/#{JWT_SECRET}/\${escapedSecret}/g" appsettings.tmp.json
                        sed -i "s/#{JWT_ISSUER}/\${escapedIssuer}/g" appsettings.tmp.json
                        sed -i "s/#{JWT_AUDIENCE}/\${escapedAudience}/g" appsettings.tmp.json
                    """
                }
            }
        }

        stage('Configure Google Auth') {
            steps {
                script {
                    sh """
                        escapedClientId=\$(echo \"${GOOGLE_CLIENT_ID}\" | sed 's/[&/\\]/\\\\&/g')
                        escapedSecret=\$(echo \"${GOOGLE_SECRET}\" | sed 's/[&/\\]/\\\\&/g')
                        
                        sed -i "s/#{GOOGLE_CLIENT_ID}/\${escapedClientId}/g" appsettings.tmp.json
                        sed -i "s/#{GOOGLE_SECRET}/\${escapedSecret}/g" appsettings.tmp.json
                    """
                }
            }
        }

        stage('Configure Gemini API') {
            steps {
                script {
                    sh """
                        escapedKey=\$(echo \"${GEMINI_API_KEY}\" | sed 's/[&/\\]/\\\\&/g')
                        escapedEndpoint=\$(echo \"${GEMINI_API_ENDPOINT}\" | sed 's/[&/\\]/\\\\&/g')
                        
                        sed -i "s/#{GEMINI_API_KEY}/\${escapedKey}/g" appsettings.tmp.json
                        sed -i "s|#{GEMINI_API_ENDPOINT}|\${escapedEndpoint}|g" appsettings.tmp.json
                    """
                }
            }
        }

        stage('Validate Configuration') {
            steps {
                script {
                    sh '''
                        # 检查 JSON 格式是否有效
                        if ! jq empty appsettings.tmp.json; then
                            echo "Error: Invalid JSON format"
                            exit 1
                        fi
                        
                        # 检查所有占位符是否都已替换
                        if grep -q "#{.*}" appsettings.tmp.json; then
                            echo "Error: Some placeholders were not replaced"
                            exit 1
                        fi
                        
                        # 替换原始配置文件
                        mv appsettings.tmp.json appsettings.json
                    '''
                }
            }
        }

        stage('Build Docker Image') {
            steps {
                script {
                    sh '''
                        docker stop job-tracker-api-container || true
                        docker rm job-tracker-api-container || true
                        docker build -t job-tracker-api .
                    '''
                }
            }
        }

        stage('Deploy') {
            steps {
                script {
                    sh """
                        docker run -d \
                            --name job-tracker-api-container \
                            -p ${API_PORT}:80 \
                            job-tracker-api
                    """
                }
            }
        }
    }
    
    post {
        success {
            echo 'Deployment successful!'
        }
        failure {
            echo 'Deployment failed. Please check the logs for details.'
            // 清理临时文件
            sh 'rm -f appsettings.tmp.json || true'
        }
        always {
            // 清理工作空间
            cleanWs()
        }
    }
}