#!/bin/bash
cd "$(dirname "$0")"

projectName="$1"
versionId="$(cat src/Plugin.cs | grep 'public const string PLUGIN_VERSION = ' | cut -d\" -f2)"
newJSON="$(cat ./assets/modinfo.json | jq ".version = \"${versionId}\"")"
if [[ "$newJSON" != "" && versionId != "" ]]
then
	printf "$newJSON" > ./assets/modinfo.json
fi

if [[ "$projectName" != "" && -d "../_resources/Rain World/RainWorld_Data/StreamingAssets/mods/${projectName}/" ]]
then
	rm -r "../_resources/Rain World/RainWorld_Data/StreamingAssets/mods/${projectName}/"
fi
