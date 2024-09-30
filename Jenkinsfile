pipeline {
    agent any
    environment {
        POSTGRES_CREDS = credentials('postgres-credentials')
        JWT_SECRET = credentials('jwt-secret')
        AUTHENTICATION_GOOGLE_CLIENTID = credentials('Authentication_Google_ClientId')
        AUTHENTICATION_GOOGLE_SECRET = credentials('Authentication_Google_ClientSecret')
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
                script {
                    sh """
                    docker build -t ${DOCKER_IMAGE_NAME} \
                    --build-arg POSTGRES_CREDS=${POSTGRES_CREDS} \
                    --build-arg JWT_KEY=${JWT_SECRET} \
                    --build-arg JWT_ISSUER=${JWT_ISSUER} \
                    --build-arg JWT_AUDIENCE=${JWT_AUDIENCE} \
                    --build-arg API_PORT=${API_PORT} \
                    --build-arg AUTHENTICATION_GOOGLE_CLIENTID=${AUTHENTICATION_GOOGLE_CLIENTID} \
                    --build-arg AUTHENTICATION_GOOGLE_SECRET=${AUTHENTICATION_GOOGLE_SECRET} \
                    .
                    """
                }
            }
        }
        stage('Deploy Docker Container') {
            steps {
                script {
                    sh """
                    docker stop ${DOCKER_CONTAINER_NAME} || true
                    docker rm ${DOCKER_CONTAINER_NAME} || true
                    docker run -d \
                    --name ${DOCKER_CONTAINER_NAME} \
                    --network host \
                    -e POSTGRES_CREDS=${POSTGRES_CREDS} \
                    -e JWT_KEY=${JWT_SECRET} \
                    -e JWT_ISSUER=${JWT_ISSUER} \
                    -e JWT_AUDIENCE=${JWT_AUDIENCE} \
                    -e API_PORT=${API_PORT} \
                    -e AUTHENTICATION_GOOGLE_CLIENTID=${AUTHENTICATION_GOOGLE_CLIENTID} \
                    -e AUTHENTICATION_GOOGLE_SECRET=${AUTHENTICATION_GOOGLE_SECRET} \
                    ${DOCKER_IMAGE_NAME}
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
        }
    }
}