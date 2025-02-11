@Library('jenkins-shared-libraries') _
def ENV_LOC=[:]
pipeline {
    parameters {
        choice(name: 'PLATFORM_FILTER', choices: ['all', 'windows-dotnet-samples', 'linux-dotnet-samples', 'mac-arm-dotnet-samples', 'mac-intel-dotnet-samples','linux-arm-dotnet-samples'], description: 'Run on specific platform')
        booleanParam defaultValue: false, description: 'Completely clean the workspace before building, including the Conan cache', name: 'CLEAN_WORKSPACE'
        booleanParam defaultValue: false, description: 'Run clean-samples', name: 'DISTCLEAN'
        booleanParam defaultValue: true, description: 'Run clean-nuget-cache', name: 'NUGETCLEAN'
    }
    options{
        buildDiscarder logRotator(artifactDaysToKeepStr: '4', artifactNumToKeepStr: '10', daysToKeepStr: '7', numToKeepStr: '10')
        disableConcurrentBuilds()
        timeout(time: 4, unit: "HOURS")
    }
    agent none
    triggers {
        // Run branches between 0800 and 0830, depending on a hash of the job name
        // This means if there's more than one branch (a feature branch, maybe?), they
        // won't all start at the same time.
        cron(env.BRANCH_NAME == "develop" ? 'H(0-30) 6 * * *' : '')
    }
    stages {
        stage('Matrix stage') {
            matrix {
                agent {
                    label "${NODE}"
                }
                when { anyOf {
                    expression { params.PLATFORM_FILTER == 'all' }
                    expression { params.PLATFORM_FILTER == env.NODE }
                } }
                axes {
                    axis {
                        name 'NODE'
                        values 'windows-dotnet-samples', 'linux-dotnet-samples', 'mac-arm-dotnet-samples', 'mac-intel-dotnet-samples','linux-arm-dotnet-samples'
                    }
                }
                environment {
                    CONAN_USER_HOME = "${WORKSPACE}"
                    CONAN_NON_INTERACTIVE = '1'
                    CONAN_PRINT_RUN_COMMANDS = '1'
                }
                stages {
                    stage('Axis'){
                        steps {
                            printPlatformNameInStep()
                        }
                    }
                    stage('Clean/reset Git checkout for release') {
                        when {
                            expression {
                                params.CLEAN_WORKSPACE
                            }
                        }
                        steps {
                            echo "Clean ${NODE}"
                            script {
                                // Ensure that the checkout is clean and any changes
                                // to .gitattributes and .gitignore have been taken
                                // into effect
                                if (isUnix()) {
                                    sh """
                                          git rm -f -q -r .
                                          git reset --hard HEAD
                                          git clean -fdx
                                    """
                                } else {
                                    // On Windows, 'git clean' can't handle long paths in .conan,
                                    // so remove that first.
                                    bat """
                                          if exist ${WORKSPACE}\\.conan\\ rmdir/s/q ${WORKSPACE}\\.conan
                                          git rm -q -r .
                                          git reset --hard HEAD
                                          git clean -fdx
                                    """
                                }
                            }
                        }
                    }
                    stage('Set-Up Environment') {
                        steps {
                            echo "Set-Up Environment ${NODE}"
                            script {
                                if (isUnix()) {
                                    sh './mkenv.py --verbose'
                                    ENV_LOC[NODE] = sh (
                                        script: './mkenv.py --env-name',
                                        returnStdout: true
                                    ).trim()
                                } else {
                                    // Using the mkenv.py script like this assumes the Python Launcher is
                                    // installed on the Windows host.
                                    // https://docs.python.org/3/using/windows.html#launcher
                                    bat '.\\mkenv.py --verbose'
                                    ENV_LOC[NODE] = bat (
                                        // The @ prevents Windows from echoing the command itself into the stdout,
                                        // which would corrupt the value of the returned data.
                                        script: '@.\\mkenv.py --env-name',
                                        returnStdout: true
                                    ).trim()
                                }
                            }
                        }
                    }
                    stage('Clean Samples') {
                        steps {
                            echo "Clean ${NODE}"
                            script {
                                if (isUnix()) {
                                    sh """. ${ENV_LOC[NODE]}/bin/activate
                                          invoke clean-samples
                                    """
                                } else {
                                    bat """CALL ${ENV_LOC[NODE]}\\Scripts\\activate
                                          invoke clean-samples
                                    """
                                }
                            }
                        }
                    }
                    stage('Clean Nuget Cache') {
                        when {
                            expression {
                                params.NUGETCLEAN
                            }
                        }
                        steps {
                            echo "Clean ${NODE}"
                            script {
                                if (isUnix()) {
                                    sh """. ${ENV_LOC[NODE]}/bin/activate
                                          invoke clean-nuget-cache
                                    """
                                } else {
                                    bat """CALL ${ENV_LOC[NODE]}\\Scripts\\activate
                                          invoke clean-nuget-cache
                                    """
                                }
                            }
                        }
                    }
                    stage('Build Samples') {
                        steps {
                            echo "Bootstrap ${NODE}"
                            script {
                                if (isUnix()) {
                                    sh """. ${ENV_LOC[NODE]}/bin/activate
                                          invoke build-samples
                                    """
                                } else {
                                    bat """CALL ${ENV_LOC[NODE]}\\Scripts\\activate
                                          invoke build-samples
                                    """
                                }
                            }
                        }
                    }
                    stage('Run Samples') {
                        steps {
                            echo "Show Conan dependencies ${NODE}"
                            script {
                                if (isUnix()) {
                                    sh """. ${ENV_LOC[NODE]}/bin/activate
                                          invoke run-samples
                                    """
                                } else {
                                    bat """CALL ${ENV_LOC[NODE]}\\Scripts\\activate
                                          invoke run-samples
                                    """
                                }
                            }
                        }
                    }
                    stage('Clean Samples After Run') {
                        steps {
                            echo "Clean ${NODE}"
                            script {
                                if (isUnix()) {
                                    sh """. ${ENV_LOC[NODE]}/bin/activate
                                          invoke clean-samples
                                    """
                                } else {
                                    bat """CALL ${ENV_LOC[NODE]}\\Scripts\\activate
                                          invoke clean-samples
                                    """
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}