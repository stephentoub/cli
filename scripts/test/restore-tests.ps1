#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. $PSScriptRoot\..\common\_common.ps1

# Restore packages
if ($Offline){
    header "Skipping test package restore"
}
else {
    header "Restoring test packages"
    & dotnet restore "$RepoRoot\test" -f "$TestPackageDir\Debug"
}
