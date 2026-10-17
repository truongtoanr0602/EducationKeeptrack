pipeline {
  agent any
  options { timestamps(); disableConcurrentBuilds() }
  environment {
    // Jenkins agent requires .NET SDK from global.json, Node 22 and Docker.
    COMPOSE_PROJECT_NAME = "ekt-ci-${BUILD_NUMBER}"
  }
  stages {
    stage('Restore and build') {
      steps {
        sh 'dotnet restore && dotnet build --no-restore -c Release'
        dir('ui') { sh 'npm ci && npm run build' }
      }
    }
    stage('API integration tests') {
      steps {
        withCredentials([string(credentialsId: 'ekt-ci-postgres-password', variable: 'POSTGRES_PASSWORD')]) {
          sh '''
            set +x
            docker compose -f compose.ci.yaml up -d --wait db
            export TEST_DATABASE_CONNECTION="Host=127.0.0.1;Port=$(docker compose -f compose.ci.yaml port db 5432 | sed 's/.*://');Database=postgres;Username=ekt_ci;Password=$POSTGRES_PASSWORD"
            dotnet test tests/ApiTests -c Release --no-build --logger 'trx;LogFileName=api.trx'
          '''
        }
      }
    }
    stage('Browser demo') {
      steps {
        withCredentials([string(credentialsId: 'ekt-ci-postgres-password', variable: 'POSTGRES_PASSWORD'), string(credentialsId: 'ekt-ci-demo-password', variable: 'DEMO_PASSWORD')]) {
          sh '''
            set +x
            export ConnectionStrings__Database="Host=127.0.0.1;Port=$(docker compose -f compose.ci.yaml port db 5432 | sed 's/.*://');Database=postgres;Username=ekt_ci;Password=$POSTGRES_PASSWORD"
            export ASPNETCORE_ENVIRONMENT=Development
            dotnet run --no-build -c Release --project src/Host -- --migrate
            export DEMO_EMAIL=ci-teacher@example.test
            export Provision__Email="$DEMO_EMAIL" Provision__Password="$DEMO_PASSWORD"
            export Provision__Until="$(date -u -d '+1 day' +%Y-%m-%dT%H:%M:%SZ)"
            dotnet run --no-build -c Release --project src/Host -- --provision-teacher
            dotnet run --no-build -c Release --no-launch-profile --project src/Host --urls http://127.0.0.1:5080 > backend-ci.log 2>&1 &
            backend_pid=$!
            cd ui
            trap 'kill "$backend_pid" 2>/dev/null || true' EXIT
            npx playwright install --with-deps chromium
            npm test
          '''
        }
      }
    }
  }
  post {
    always {
      withCredentials([string(credentialsId: 'ekt-ci-postgres-password', variable: 'POSTGRES_PASSWORD')]) {
        sh 'docker compose -f compose.ci.yaml down -v'
      }
      archiveArtifacts artifacts: 'tests/**/TestResults/*.trx', allowEmptyArchive: true
    }
  }
}
