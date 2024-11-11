pipeline {
    agent any
    
    environment {
        // sensitive environment variables
        POSTGRES_CREDS = credentials('postgres-credentials')
        JWT_SECRET = credentials('jwt-secret')
        GOOGLE_AUTH = credentials('Authentication_Google_ClientId')
        GOOGLE_SECRET = credentials('Authentication_Google_ClientSecret')
        
        // non-sensitive environment variables
        JWT_ISSUER = 'JobTrackerAPI'
        JWT_AUDIENCE = 'JobTrackerClient'
        API_PORT = '5052'
        DOCKER_IMAGE_NAME = 'job-tracker-api'
        DOCKER_CONTAINER_NAME = 'job-tracker-api-container'
    }
    
    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }
        
        stage('Build Docker Image') {
            steps {
                withCredentials([
                    usernamePassword(credentialsId: 'postgres-credentials', usernameVariable: 'DB_USER', passwordVariable: 'DB_PASS'),
                    string(credentialsId: 'jwt-secret', variable: 'JWT_KEY'),
                    string(credentialsId: 'Authentication_Google_ClientId', variable: 'GOOGLE_CLIENT_ID'),
                    string(credentialsId: 'Authentication_Google_ClientSecret', variable: 'GOOGLE_CLIENT_SECRET')
                ]) {
                    sh '''
                        docker build -t ${DOCKER_IMAGE_NAME} \
                        --build-arg POSTGRES_USER="$DB_USER" \
                        --build-arg POSTGRES_PASS="$DB_PASS" \
                        --build-arg JWT_KEY="$JWT_KEY" \
                        --build-arg JWT_ISSUER="$JWT_ISSUER" \
                        --build-arg JWT_AUDIENCE="$JWT_AUDIENCE" \
                        --build-arg API_PORT="$API_PORT" \
                        --build-arg GOOGLE_CLIENT_ID="$GOOGLE_CLIENT_ID" \
                        --build-arg GOOGLE_CLIENT_SECRET="$GOOGLE_CLIENT_SECRET" \
                        .
                    '''
                }
            }
        }
        
        stage('Deploy Docker Container') {
            steps {
                withCredentials([
                    usernamePassword(credentialsId: 'postgres-credentials', usernameVariable: 'DB_USER', passwordVariable: 'DB_PASS'),
                    string(credentialsId: 'jwt-secret', variable: 'JWT_KEY'),
                    string(credentialsId: 'Authentication_Google_ClientId', variable: 'GOOGLE_CLIENT_ID'),
                    string(credentialsId: 'Authentication_Google_ClientSecret', variable: 'GOOGLE_CLIENT_SECRET')
                ]) {
                    sh '''
                        docker stop ${DOCKER_CONTAINER_NAME} || true
                        docker rm ${DOCKER_CONTAINER_NAME} || true
                        docker run -d \
                        --name ${DOCKER_CONTAINER_NAME} \
                        --network host \
                        -e POSTGRES_USER="$DB_USER" \
                        -e POSTGRES_PASS="$DB_PASS" \
                        -e JWT_KEY="$JWT_KEY" \
                        -e JWT_ISSUER="$JWT_ISSUER" \
                        -e JWT_AUDIENCE="$JWT_AUDIENCE" \
                        -e API_PORT="$API_PORT" \
                        -e GOOGLE_CLIENT_ID="$GOOGLE_CLIENT_ID" \
                        -e GOOGLE_CLIENT_SECRET="$GOOGLE_CLIENT_SECRET" \
                        ${DOCKER_IMAGE_NAME}
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