pipeline {
  agent { node { label 'centos7-mono' } }
  stages {
    stage('Install tools') {
      steps {
        sh 'sudo yum -y install rpm-build redhat-rpm-config rpmdevtools yum-utils'
      }
    }
    stage('Build CDAB Client') {
      steps {
        echo 'Building CDAB Client'
        dir("src/cdab-client") {
          load 'Jenkinsfile'
        }
      }
    }
    stage('Publish') {
      steps {
        dir("src/cdab-client") {
          archiveArtifacts artifacts: 'build/RPMS/**/*.rpm', fingerprint: true
          echo 'Deploying'
          script {
              // Obtain an Artifactory server instance, defined in Jenkins --> Manage:
              def server = Artifactory.server 'repository.terradue.com'

              // Read the upload specs:
              def uploadSpec = readFile 'artifactdeploy.json'

              // Upload files to Artifactory:
              def buildInfo = server.upload spec: uploadSpec

              // Publish the merged build-info to Artifactory
              server.publishBuildInfo buildInfo
          }
        }
      }
    }
  }
}
