#!/bin/bash
cd "$(dirname "$0")"

versionId="$(cat src/Plugin.cs | grep 'public const string PLUGIN_VERSION = ' | cut -d\" -f2)"
newJSON="$(cat ./assets/modinfo.json | jq ".version = \"${versionId}\"")"
if [[ "$newJSON" != "" && versionId != "" ]]
then
	printf "$newJSON" > ./assets/modinfo.json
fi
