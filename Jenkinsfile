pipeline {
    agent any

    environment {
        POSTGRES_CREDS = credentials('postgres-credentials')
        JWT_SECRET = credentials('jwt-secret')
        JWT_ISSUER = 'JobTrackerAPI'
        JWT_AUDIENCE = 'JobTrackerClient'
        API_PORT = '5052'
        DOCKER_COMPOSE_FILE = 'docker-compose.yml'
        DOCKER_COMPOSE_PATH = '/usr/local/bin/docker-compose'
        // Add Docker registry credentials if needed
        DOCKER_CREDS = credentials('docker-registry-credentials')
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

        stage('Docker Login') {
            steps {
                script {
                    // Login to Docker registry if needed
                    sh 'echo $DOCKER_CREDS_PSW | docker login -u $DOCKER_CREDS_USR --password-stdin'
                }
            }
        }

        stage('Deploy with Docker Compose') {
            steps {
                script {
                    sh '${DOCKER_COMPOSE_PATH} -f $DOCKER_COMPOSE_FILE --env-file .env up -d'
                }
            }
        }
    }

    post {
        always {
            sh 'rm -f .env'
            // Logout from Docker registry
            sh 'docker logout'
        }
        success {
            echo 'Deployment successful!'
        }
        failure {
            echo 'Deployment failed. Please check the logs for details.'
        }
    }
}