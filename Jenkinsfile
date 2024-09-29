pipeline {
    agent any

    environment {
        POSTGRES_CREDS = credentials('postgres-credentials')
        JWT_SECRET = credentials('jwt-secret')
        JWT_ISSUER = 'JobTrackerAPI'
        JWT_AUDIENCE = 'JobTrackerClient'
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
                    // Create a .env file with the secrets
                    sh '''
                        echo "POSTGRES_USER=$POSTGRES_CREDS_USR" > .env
                        echo "POSTGRES_PASSWORD=$POSTGRES_CREDS_PSW" >> .env
                        echo "JWT_KEY=$JWT_SECRET" >> .env
                        echo "JWT_ISSUER=$JWT_ISSUER" >> .env
                        echo "JWT_AUDIENCE=$JWT_AUDIENCE" >> .env
                    '''
                }
            }
        }

        stage('Deploy with Docker Compose') {
            steps {
                script {
                    // Use the .env file and run docker-compose
                    sh '/usr/local/bin/docker-compose -f docker-compose.yml --env-file .env up -d'

                }
            }
        }
    }

    post {
        always {
            // Clean up the .env file
            sh 'rm -f .env'
        }
        success {
            echo 'Deployment successful!'
        }
        failure {
            echo 'Deployment failed. Please check the logs for details.'
        }
    }
}