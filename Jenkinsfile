pipeline {
    agent any

    environment {
        POSTGRES_CREDS = credentials('postgres-credentials')
        JWT_SECRET = credentials('jwt-secret')
        JWT_ISSUER = 'JobTrackerAPI'
        JWT_AUDIENCE = 'JobTrackerClient'
        API_PORT = '5503'
        DOCKER_COMPOSE_FILE = 'docker-compose.yml'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Setup Environment') {
            steps {
                script {
                    // Create .env file
                    writeFile file: '.env', text: """
                        POSTGRES_USER=${POSTGRES_CREDS_USR}
                        POSTGRES_PASSWORD=${POSTGRES_CREDS_PSW}
                        JWT_KEY=${JWT_SECRET}
                        JWT_ISSUER=${JWT_ISSUER}
                        JWT_AUDIENCE=${JWT_AUDIENCE}
                        API_PORT=${API_PORT}
                    """
                }
            }
        }

        stage('Deploy with Docker Compose') {
            steps {
                script {
                    // Deploy using Docker Compose
                    sh '/usr/local/bin/docker-compose -f ${DOCKER_COMPOSE_FILE} --env-file .env up -d'
                }
            }
        }
    }

    post {
        always {
            script {
                // Clean up
                sh 'rm -f .env'
            }
        }
        success {
            echo 'Deployment successful!'
        }
        failure {
            echo 'Deployment failed. Please check the logs for details.'
        }
    }
}