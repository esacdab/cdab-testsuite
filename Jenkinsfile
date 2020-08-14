

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
                            env.release_cdab_client = env.BUILD_NUMBER
                        }
                        else {
                            env.release_cdab_client = 'SNAPSHOT' + sdf
                        }
                    }
                    echo 'Build package'
                    sh "rpmbuild --define \"_topdir ${pwd()}/build\" -ba --define '_branch ${env.BRANCH_NAME}' --define '_release ${env.release_cdab_client}' build/SPECS/cdab-client.spec"
                    sh "rpm -qpl ${pwd()}/build/RPMS/*/*.rpm"
                }
                stash includes: 'src/cdab-client/build/RPMS/**/*.rpm', name: 'cdab-client-rpm'
            }
        }
        stage('Package CDAB Remote Client') {
            agent { 
                docker { 
                    image 'alectolytic/rpmbuilder:centos-7' 
                } 
            }
            steps {
                dir("src/cdab-remote-client") {
                    sh 'mkdir -p build/{BUILD,RPMS,SOURCES,SPECS,SRPMS}'
                    sh 'cp cdab-remote-client.spec build/SPECS/cdab-remote-client.spec'
                    sh 'spectool -g -R --directory build/SOURCES build/SPECS/cdab-remote-client.spec'
                    sh 'cp -r bin build/SOURCES/'
                    sh 'cp -r libexec build/SOURCES/'
                    sh 'cp -r etc build/SOURCES/'
                    script {
                        def sdf = sh(returnStdout: true, script: 'date -u +%Y%m%dT%H%M%S').trim()
                        if (env.BRANCH_NAME == 'master') {
                            env.release = env.BUILD_NUMBER
                        }
                        else {
                            env.release = 'SNAPSHOT' + sdf
                        }
                    }
                    echo 'Build package'
                    sh "rpmbuild --define \"_topdir ${pwd()}/build\" -ba --define '_branch ${env.BRANCH_NAME}' --define '_release ${env.release}' build/SPECS/cdab-remote-client.spec"
                    sh "rpm -qpl ${pwd()}/build/RPMS/*/*.rpm"
                }
                stash includes: 'src/cdab-remote-client/build/RPMS/**/*.rpm', name: 'cdab-remote-client-rpm'
            }
        }
        stage('Build & Publish RPMs') {
            steps {
                unstash name: 'cdab-client-rpm'
                unstash name: 'cdab-remote-client-rpm'
                archiveArtifacts artifacts: 'src/*/build/RPMS/**/*.rpm', fingerprint: true
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

        stage('Build & Publish Docker') {
            steps {
                script {
                    def descriptor = readDescriptor()
                    def testsuite = docker.build(descriptor.docker_image_name, "--build-arg CDAB_RELEASE=${${descriptor.version}} --build-arg CDAB_CLIENT_RPM=cdab-client-${env.release_cdab_client}.noarch.rpm ./docker")
                    def mType=getTypeOfVersion(env.BRANCH_NAME)
                    testsuite.push('${mType}${${descriptor.version}}')
                    testsuite.push('${mType}latest')
                }
            }
        }
    }
}

def getTypeOfVersion(branchName) {
  
  def matcher = (env.BRANCH_NAME =~ /master/)
  if (matcher.matches())
    return ""
  
  return "dev"
}

def readDescriptor (){
    return readYaml(file: 'build.yml')
}