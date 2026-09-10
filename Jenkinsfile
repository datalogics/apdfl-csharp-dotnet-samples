@Library('jenkins-shared-libraries') _
def ENV_LOC=[:]

// Per-job NuGet cache root, so concurrent jobs on a node don't contend for
// the shared per-user cache. On Unix the workspace is already per-job, so
// the cache lives inside it; on Windows it is rooted at the drive root
// (like setConanHome) to keep restored package paths under the long-path
// limit.
def setNugetRoot() {
    if (isUnix()) {
        return "${WORKSPACE}/.nuget"
    }
    def jobDirectory = "DL\\" + env.JOB_NAME.tokenize('/')[-2] + "_" + env.JOB_BASE_NAME
    return getWindowsRootDrive() + jobDirectory + "\\.nuget"
}

def nugetCachePath(String subdir) {
    return setNugetRoot() + (isUnix() ? '/' : '\\') + subdir
}

pipeline {
    parameters {
        choice(name: 'PLATFORM_FILTER', choices: ['all', 'windows-dotnet-samples', 'rocky9-dotnet-samples', 'mac-arm-dotnet-samples', 'mac-intel-dotnet-samples', 'rocky9-arm-dotnet-samples'], description: 'Run on specific platform')
        booleanParam defaultValue: false, description: 'Completely clean the workspace before building, including the NuGet cache', name: 'CLEAN_WORKSPACE'
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
        cron(env.BRANCH_NAME == "develop-21" ? '30 7 * * *' : '')
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
                        values 'windows-dotnet-samples', 'rocky9-dotnet-samples', 'mac-arm-dotnet-samples', 'mac-intel-dotnet-samples','rocky9-arm-dotnet-samples'
                    }
                }
                environment {
                    // NuGet honors these for restore, build, and
                    // 'dotnet nuget locals --clear all' alike.
                    NUGET_ROOT = setNugetRoot()
                    NUGET_PACKAGES = nugetCachePath('packages')
                    NUGET_HTTP_CACHE_PATH = nugetCachePath('http-cache')
                    NUGET_PLUGINS_CACHE_PATH = nugetCachePath('plugins-cache')
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
                                    // On Windows the NuGet cache root lives outside
                                    // the workspace, so git clean can't remove it.
                                    bat """
                                          if exist "%NUGET_ROOT%" rmdir /s /q "%NUGET_ROOT%"
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
                    stage('Build Samples using Nightly packages') {
                        steps {
                            echo "Build the samples ${NODE}"
                            script {
                                if (isUnix()) {
                                    sh """. ${ENV_LOC[NODE]}/bin/activate
                                          invoke build-samples --pkg-source Nightly
                                    """
                                } else {
                                    bat """CALL ${ENV_LOC[NODE]}\\Scripts\\activate
                                          invoke build-samples --pkg-source Nightly
                                    """
                                }
                            }
                        }
                    }
                    stage('Run Samples using Nightly packages') {
                        steps {
                            echo "Run the samples ${NODE}"
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
                    stage('Clean Samples After Nightly Run') {
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
                    stage('Clean Nuget Packages Before Public Build') {
                        steps {
                            echo "Clean ${NODE}"
                            script {
                                if (isUnix()) {
                                    sh """. ${ENV_LOC[NODE]}/bin/activate
                                          invoke clean-nuget-packages
                                    """
                                } else {
                                    bat """CALL ${ENV_LOC[NODE]}\\Scripts\\activate
                                          invoke clean-nuget-packages
                                    """
                                }
                            }
                        }
                    }

                    stage('Build Samples using Public packages') {
                        steps {
                            echo "Build the samples ${NODE}"
                            script {
                                if (isUnix()) {
                                    sh """. ${ENV_LOC[NODE]}/bin/activate
                                          invoke build-samples --pkg-source Public
                                    """
                                } else {
                                    bat """CALL ${ENV_LOC[NODE]}\\Scripts\\activate
                                          invoke build-samples --pkg-source Public
                                    """
                                }
                            }
                        }
                    }
                    stage('Run Samples using Public packages') {
                        steps {
                            echo "Run the samples ${NODE}"
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
                    stage('Clean Samples After Public Run') {
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
