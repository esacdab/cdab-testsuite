pipeline {
    agent none
    stages {
        stage('Build CDAB client') {
            agent { 
                docker { 
                    image 'mono:6.8' 
                    args '-u root:sudo'
                } 
            }
            steps {
                sh 'ls $WORKSPACE'
                dir("src/cdab-client") {
                    echo 'Build CDAB client .NET application'
                    sh 'msbuild /t:build /Restore:true /p:Configuration=DEBUG'
                    stash includes: 'bin/**,App_Data/**,cdab-client', name: 'cdab-client-build'
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
