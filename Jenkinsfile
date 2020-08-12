#!groovy

node {
    stage('Check Out') {
        echo 'Shared stage'

        checkout scm
    }

    dir("src/cdab-client") {
        load 'Jenkinsfile'
    }
    // load 'Project2/Jenkinsfile'
}
