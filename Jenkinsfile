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
        stage('Deploy') {
            steps {
                script {
                    sh '''
                        # 替换配置文件中的占位符
                        sed -i "s/#{DB_HOST}/$DB_HOST/g" appsettings.json
                        sed -i "s/#{DB_NAME}/$DB_NAME/g" appsettings.json
                        sed -i "s/#{DB_USERNAME}/$DB_CREDS_USR/g" appsettings.json
                        sed -i "s/#{DB_PASSWORD}/$DB_CREDS_PSW/g" appsettings.json
                        sed -i "s/#{DB_PORT}/$DB_PORT/g" appsettings.json
                        
                        sed -i "s/#{JWT_SECRET}/$JWT_SECRET/g" appsettings.json
                        sed -i "s/#{JWT_ISSUER}/$JWT_ISSUER/g" appsettings.json
                        sed -i "s/#{JWT_AUDIENCE}/$JWT_AUDIENCE/g" appsettings.json
                        
                        sed -i "s/#{GOOGLE_CLIENT_ID}/$GOOGLE_CLIENT_ID/g" appsettings.json
                        sed -i "s/#{GOOGLE_SECRET}/$GOOGLE_SECRET/g" appsettings.json
                        
                        sed -i "s/#{GEMINI_API_KEY}/$GEMINI_API_KEY/g" appsettings.json
                        sed -i "s|#{GEMINI_API_ENDPOINT}|$GEMINI_API_ENDPOINT|g" appsettings.json
                        
                        # Docker 构建和运行
                        docker build -t job-tracker-api .
                        docker run -d \
                            --name job-tracker-api-container \
                            -p $API_PORT:80 \
                            job-tracker-api
                    '''
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
        }
    }
}