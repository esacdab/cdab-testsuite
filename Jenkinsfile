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
                    args '-u root'
                } 
            }
            steps {
                dir("src/cdab-client") {
                    unstash name: 'cdab-client-build'
                    sh 'mkdir -p $WORKSPACE/build/{BUILD,RPMS,SOURCES,SPECS,SRPMS}'
                    sh 'cp cdab-client.spec $WORKSPACE/build/SPECS/cdab-client.spec'
                    sh 'spectool -g -R --directory $WORKSPACE/build/SOURCES $WORKSPACE/build/SPECS/cdab-client.spec'
                    sh 'cp -r bin $WORKSPACE/build/SOURCES/'
                    sh 'cp -r App_Data $WORKSPACE/build/SOURCES/'
                    sh 'cp cdab-client $WORKSPACE/build/SOURCES/'
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
                    sh "sudo yum-builddep -y $WORKSPACE/build/SPECS/cdab-client.spec"
                    echo 'Build package'
                    sh "sudo rpmbuild --define \"_topdir $WORKSPACE/build\" -ba --define '_branch ${env.BRANCH_NAME}' --define '_release ${env.release}' $WORKSPACE/build/SPECS/cdab-client.spec"
                    sh "rpm -qpl $WORKSPACE/build/RPMS/*/*.rpm"
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
