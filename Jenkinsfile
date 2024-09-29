pipeline {
    agent any

    environment {
        POSTGRES_CREDS = credentials('postgres-credentials')
        JWT_SECRET = credentials('jwt-secret')
        JWT_ISSUER = 'JobTrackerAPI'
        JWT_AUDIENCE = 'JobTrackerClient'
        API_PORT = '5503'
        DOCKER_IMAGE_NAME = 'jobtracker-backend'
        DOCKER_IMAGE_TAG = "${BUILD_NUMBER}"
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
                    docker.build("${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}")
                }
            }
        }

        stage('Deploy') {
            steps {
                script {
                    sh """
                        docker stop ${DOCKER_IMAGE_NAME} || true
                        docker rm ${DOCKER_IMAGE_NAME} || true
                        docker run -d --name ${DOCKER_IMAGE_NAME} \
                            -p ${API_PORT}:80 \
                            -e ASPNETCORE_ENVIRONMENT=Production \
                            -e ConnectionStrings__DefaultConnection="Host=your_db_host;Database=JobTracker;Username=${POSTGRES_CREDS_USR};Password=${POSTGRES_CREDS_PSW}" \
                            -e Jwt__Key=${JWT_SECRET} \
                            -e Jwt__Issuer=${JWT_ISSUER} \
                            -e Jwt__Audience=${JWT_AUDIENCE} \
                            ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}
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