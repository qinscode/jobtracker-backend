pipeline {
    agent any

    environment {
        POSTGRES_CREDS = credentials('postgres-credentials')
        JWT_SECRET = credentials('jwt-secret')
        JWT_ISSUER = 'JobTrackerAPI'
        JWT_AUDIENCE = 'JobTrackerClient'
        API_PORT = '5052'
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
                    sh '''
                        echo "POSTGRES_USER=$POSTGRES_CREDS_USR" > .env
                        echo "POSTGRES_PASSWORD=$POSTGRES_CREDS_PSW" >> .env
                        echo "JWT_KEY=$JWT_SECRET" >> .env
                        echo "JWT_ISSUER=$JWT_ISSUER" >> .env
                        echo "JWT_AUDIENCE=$JWT_AUDIENCE" >> .env
                        echo "API_PORT=$API_PORT" >> .env
                    '''
                }
            }
        }

        stage('Deploy with Docker Compose') {
            steps {
                script {
                    // Deploy using Docker Compose
                    sh '/usr/local/bin/docker-compose -f docker-compose.yml --env-file .env up -d'
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