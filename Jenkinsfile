#!groovy

node {
    stage('Check Out') {
        echo 'Shared stage'

        checkout scm
    }

    load 'src/cdab-client/Jenkinsfile'
    // load 'Project2/Jenkinsfile'
}
