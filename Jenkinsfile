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
        DOCKER_COMPOSE_FILE = 'docker-compose.yml'
        DOCKER_COMPOSE_PATH = '/usr/local/bin/docker-compose'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Setup and Deploy') {
            steps {
                script {
                    // Setup Environment
                    sh '''
                        echo "POSTGRES_CREDS=$POSTGRES_CREDS" > .env
                        echo "JWT_KEY=$JWT_SECRET" >> .env
                        echo "JWT_ISSUER=$JWT_ISSUER" >> .env
                        echo "JWT_AUDIENCE=$JWT_AUDIENCE" >> .env
                        echo "API_PORT=$API_PORT" >> .env
                        echo "AUTHENTICATION_GOOGLE_CLIENTID=$AUTHENTICATION_GOOGLE_CLIENTID" >> .env
                        echo "AUTHENTICATION_GOOGLE_SECRET=$AUTHENTICATION_GOOGLE_SECRET" >> .env
                    '''

                    // Deploy with Docker Compose
                    sh "${DOCKER_COMPOSE_PATH} -f ${DOCKER_COMPOSE_FILE} --env-file .env up -d"
                }
            }
        }
    }

    post {
        always {
            sh 'rm -f .env'
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