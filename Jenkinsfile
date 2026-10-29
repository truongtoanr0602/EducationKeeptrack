pipeline {
  agent any
  options { timestamps(); disableConcurrentBuilds(); timeout(time: 30, unit: 'MINUTES') }
  stages {
    stage('Prepare run') {
      steps {
        script {
          def id = java.util.UUID.randomUUID().toString().replace('-', '')
          env.R2CI_DIR = ".local/ci/${id}"
          env.COMPOSE_PROJECT_NAME = "ekt-ci-${id}"
          env.PLAYWRIGHT_BROWSERS_PATH = "${env.WORKSPACE}/${env.R2CI_DIR}/browsers"
        }
        sh 'mkdir -p "$R2CI_DIR" && chmod 700 "$R2CI_DIR"'
      }
    }
    stage('Restore and build') {
      steps {
        sh 'dotnet restore && dotnet build --no-restore -c Release'
        dir('ui') { sh 'npm ci && npm run typecheck && npm run build' }
      }
    }
    stage('Install PDF and UI browsers') {
      steps {
        // Linux agent needs root/passwordless sudo for Playwright system dependencies.
        sh 'dotnet tools/R2Ci/bin/Release/net10.0/R2Ci.dll browser'
        dir('ui') { sh 'npx playwright install --with-deps chromium' }
      }
    }
    stage('R2 API and browser integration') {
      steps {
        withCredentials([
          string(credentialsId: 'ekt-ci-postgres-password', variable: 'POSTGRES_PASSWORD'),
          string(credentialsId: 'ekt-ci-demo-password', variable: 'DEMO_PASSWORD'),
          string(credentialsId: 'ekt-ci-r2-endpoint', variable: 'TEST_S3_ENDPOINT'),
          string(credentialsId: 'ekt-ci-r2-bucket', variable: 'TEST_S3_BUCKET'),
          string(credentialsId: 'ekt-ci-r2-access-key', variable: 'TEST_S3_ACCESS_KEY'),
          string(credentialsId: 'ekt-ci-r2-secret-key', variable: 'TEST_S3_SECRET_KEY')
        ]) {
          sh 'bash tools/ci/r2-tests.sh'
        }
      }
    }
  }
  post {
    always {
      script {
        // Independently attempt both cleanups, even when tests or credentials failed.
        def storageStatus = 1
        def databaseStatus = 1
        try {
          withCredentials([
            string(credentialsId: 'ekt-ci-r2-endpoint', variable: 'TEST_S3_ENDPOINT'),
            string(credentialsId: 'ekt-ci-r2-bucket', variable: 'TEST_S3_BUCKET'),
            string(credentialsId: 'ekt-ci-r2-access-key', variable: 'TEST_S3_ACCESS_KEY'),
            string(credentialsId: 'ekt-ci-r2-secret-key', variable: 'TEST_S3_SECRET_KEY')
          ]) {
            storageStatus = sh(returnStatus: true, script: '''
              set +x
              set -eu
              test -n "${R2CI_DIR:-}"
              dotnet tools/R2Ci/bin/Release/net10.0/R2Ci.dll cleanup "$R2CI_DIR/state.json" "$R2CI_DIR/cleanup.json"
            ''')
          }
        } catch (ignored) { echo 'R2 cleanup did not complete; use the documented recovery procedure.' }
        try {
          withCredentials([string(credentialsId: 'ekt-ci-postgres-password', variable: 'POSTGRES_PASSWORD')]) {
            databaseStatus = sh(returnStatus: true, script: '''
              set +x
              set -eu
              test -n "${COMPOSE_PROJECT_NAME:-}"
              docker compose -f compose.ci.yaml down -v > /dev/null 2>&1
            ''')
          }
        } catch (ignored) { echo 'CI database cleanup did not complete.' }
        def evidenceStatus = sh(returnStatus: true, script: '''
          set +x
          set -eu
          test -n "${R2CI_DIR:-}"
          dotnet tools/R2Ci/bin/Release/net10.0/R2Ci.dll evidence "$R2CI_DIR/state.json" "$R2CI_DIR/api.trx" "$R2CI_DIR/browser.json" "$R2CI_DIR/cleanup.json" "$R2CI_DIR/evidence.json"
        ''')
        if (storageStatus != 0 || databaseStatus != 0 || evidenceStatus != 0) {
          currentBuild.result = 'FAILURE'
        }
        // Raw logs/reports, PDFs and browser traces are never archived.
        if (env.R2CI_DIR) {
          archiveArtifacts artifacts: "${env.R2CI_DIR}/evidence.json", allowEmptyArchive: false
        }
      }
    }
  }
}
