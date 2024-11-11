pipeline {
    agent any
    
    environment {
        DOTNET_ENVIRONMENT = 'Production'
        DOCKER_IMAGE_NAME = 'job-tracker-api'
        DOCKER_CONTAINER_NAME = 'job-tracker-api-container'
        DOCKER_IMAGE_TAG = "${BUILD_NUMBER}"
        
        // Database configuration
        DB_HOST = credentials('DB_HOST')
        DB_USERNAME = credentials('DB_USERNAME')
        DB_CREDS = credentials('DB_PASSWORD')
        DB_DATABASE = credentials('DB_DATABASE')
        
        // API configuration
        API_PORT = credentials('API_PORT')
        
        // JWT configuration
        JWT_SECRET = credentials('jwt-secret')
        JWT_ISSUER = credentials('JWT_ISSUER')
        JWT_AUDIENCE = credentials('JWT_AUDIENCE')
        
        // Google OAuth configuration
        GOOGLE_CLIENT_ID = credentials('Authentication_Google_ClientId')
        GOOGLE_SECRET = credentials('Authentication_Google_ClientSecret')
        
        // Gemini API configuration
        GEMINI_API_KEY = credentials('GEMINI_API_KEY')
        GEMINI_API_ENDPOINT = credentials('GEMINI_API_ENDPOINT')
        

    }
    
    options {
        timeout(time: 30, unit: 'MINUTES')
        disableConcurrentBuilds()
        buildDiscarder(logRotator(numToKeepStr: '10'))
    }
    
    stages {
        stage('Environment Validation') {
            steps {
                script {
                    // Validate required environment variables and credentials
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
                    // Add error handling
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
                        error "Configuration file processing failed: ${e.message}"
                    }
                }
            }
        }
        
        stage('Build Docker Image') {
            steps {
                script {
                    try {
                        // Add build parameters to improve build performance
                        sh """
                            DOCKER_BUILDKIT=1 docker build \
                                --build-arg BUILDKIT_INLINE_CACHE=1 \
                                --cache-from ${DOCKER_IMAGE_NAME}:latest \
                                -t ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG} .
                            
                            docker tag ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG} ${DOCKER_IMAGE_NAME}:latest
                        """
                    } catch (Exception e) {
                        error "Docker build failed: ${e.message}"
                    }
                }
            }
        }
        
        stage('Container Health Check') {
            steps {
                script {
                    def maxRetries = 5
                    def retryInterval = 3
                    
                    // Stop and remove old container
                    sh """
                        if docker ps -a | grep -q ${DOCKER_CONTAINER_NAME}; then
                            docker stop ${DOCKER_CONTAINER_NAME} || true
                            docker rm ${DOCKER_CONTAINER_NAME} || true
                        fi
                    """
                    
                    // Start new container
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
                    
                    // Wait for container health check
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
                        // Keep the latest 3 versions of images, add error handling
                        sh """
                            docker images ${DOCKER_IMAGE_NAME} --format '{{.ID}} {{.CreatedAt}}' | \
                            sort -k2 -r | \
                            awk 'NR>3 {print \$1}' | \
                            xargs -r docker rmi -f || true
                        """
                    } catch (Exception e) {
                        echo "Warning during cleanup process: ${e.message}"
                    }
                }
            }
        }
    }
    
    post {
        always {
            script {
                // Clean up sensitive files and build artifacts
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
                // Get container status information
                def containerInfo = sh(script: """
                    echo "Container status: \$(docker ps -f name=${DOCKER_CONTAINER_NAME} --format '{{.Status}}')"
                    echo "Running ports: \$(docker port ${DOCKER_CONTAINER_NAME})"
                """, returnStdout: true).trim()
                
                echo """
===================================================================================
                                BUILD SUCCESS
===================================================================================
Build Details:
-------------
Project: ${env.JOB_NAME}
Build Number: ${env.BUILD_NUMBER}
Container Name: ${DOCKER_CONTAINER_NAME}
Image Version: ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}

Deployment Information:
----------------------
${containerInfo}

Build Information:
-----------------
Build Time: ${new Date().format("yyyy-MM-dd HH:mm:ss")}
Duration: ${currentBuild.durationString}

View Build Log: ${env.BUILD_URL}console
===================================================================================
"""
            }
        }
                
        failure {
            script {
                echo '=== Deployment Failed - Collecting Diagnostic Information ==='
                
                // Collect diagnostic information
                def diagnosticInfo = ""
                try {
                    diagnosticInfo = sh(script: """
                        echo "=== Docker Container Logs ==="
                        docker logs ${DOCKER_CONTAINER_NAME} 2>&1 || echo 'Unable to get container logs'
                        
                        echo -e "\n=== Container Status ==="
                        docker inspect ${DOCKER_CONTAINER_NAME} 2>&1 || echo 'Unable to get container status'
                        
                        echo -e "\n=== System Resource Status ==="
                        df -h
                        free -m
                        docker system df
                    """, returnStdout: true).trim()
                } catch (Exception e) {
                    diagnosticInfo = "Error collecting diagnostic information: ${e.message}"
                }
                
                echo """
===================================================================================
                                BUILD FAILED
===================================================================================
Build Details:
-------------
Project: ${env.JOB_NAME}
Build Number: ${env.BUILD_NUMBER}
Failed Stage: ${currentBuild.currentResult}

Build Information:
-----------------
Start Time: ${new Date(currentBuild.startTimeInMillis).format("yyyy-MM-dd HH:mm:ss")}
Duration: ${currentBuild.durationString}

Diagnostic Information:
----------------------
${diagnosticInfo}

View Complete Build Log: ${env.BUILD_URL}console

ACTION REQUIRED: Please check the failure reason and handle it as soon as possible!
===================================================================================
"""
            }
        }
    }
}