pipeline {
    agent any
    stages {
        stage('Build CDAB client') {
            agent { 
                docker { 
                    image 'mono:6.8' 
                } 
            }
            environment {
                HOME = '$WORKSPACE'
            }
            steps {
                dir("src/cdab-client") {
                    echo 'Build CDAB client .NET application'
                    sh 'msbuild /t:build /Restore:true /p:Configuration=DEBUG'
                    stash includes: 'bin/**,App_Data/**,cdab-client', name: 'cdab-client-build'
                }
            }
        }
        stage('Package CDAB Client') {
            agent { 
                docker { 
                    image 'alectolytic/rpmbuilder:centos-7' 
                } 
            }
            steps {
                dir("src/cdab-client") {
                    unstash name: 'cdab-client-build'
                    sh 'mkdir -p build/{BUILD,RPMS,SOURCES,SPECS,SRPMS}'
                    sh 'cp cdab-client.spec build/SPECS/cdab-client.spec'
                    sh 'spectool -g -R --directory build/SOURCES build/SPECS/cdab-client.spec'
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
                    // sh "yum-builddep -y build/SPECS/cdab-client.spec"
                    echo 'Build package'
                    sh "rpmbuild --define \"_topdir ${pwd()}/build\" -ba --define '_branch ${env.BRANCH_NAME}' --define '_release ${env.release}' build/SPECS/cdab-client.spec"
                    sh "rpm -qpl ${pwd()}/build/RPMS/*/*.rpm"
                }
            }
        }
        stage('Publish RPMs') {
            steps {
                sh "pwd"
                sh "ls"
                archiveArtifacts artifacts: 'src/cdab-client/build/RPMS/**/*.rpm', fingerprint: true
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
