pipeline {
    agent none
    stages {
        stage('Build CDAB client') {
            agent { 
                docker { 
                    image 'mono:6.8' 
                    // args '-u root:sudo'
                } 
            }
            steps {
                dir("src/cdab-client") {
                    echo 'Build CDAB client .NET application'
                    sh 'msbuild /t:restore /p:RestoreConfigFile=NuGet.Config'
                    sh 'msbuild /t:build /p:Configuration=DEBUG'
                    // stash includes: 'bin/**,App_Data/**,cdab-client', name: 'cdab-client-build'
                }
            }
        }
        stage('Package CDAB Client') {
            agent { 
                docker { 
                    image 'alectolytic/rpmbuilder:centos-7' 
                    // args '-u root'
                } 
            }
            steps {
                dir("src/cdab-client") {
                    // unstash name: 'cdab-client-build'
                    sh 'mkdir -p build/{BUILD,RPMS,SOURCES,SPECS,SRPMS}'
                    sh 'cp cdab-client.spec build/SPECS/cdab-client.spec'
                    sh 'spectool -g -R --directory build/SOURCES $WORKSPACE/build/SPECS/cdab-client.spec'
                    sh 'cp -r bin build/SOURCES/'
                    sh 'cp -r App_Data build/SOURCES/'
                    sh 'cp cdab-client build/SOURCES/'
                    script {
                        def sdf = sh(returnStdout: true, script: 'date -u +%Y%m%dT%H%M%S').trim()
                        if (env.BRANCH_NAME == 'master') {
                            env.release = env.BUILD_NUMBER
                        }
                        else {
                            env.release = 'SNAPSHOT' + sdf
                        }
                    }
                    echo 'Build package dependencies'
                    sh "sudo yum-builddep -y build/SPECS/cdab-client.spec"
                    echo 'Build package'
                    sh "sudo rpmbuild --define \"_topdir build\" -ba --define '_branch ${env.BRANCH_NAME}' --define '_release ${env.release}' build/SPECS/cdab-client.spec"
                    sh "rpm -qpl build/RPMS/*/*.rpm"
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
