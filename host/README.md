# Host

Google Forms and Google Slides are used to share Vestiges between different players.

 - [Google Forms](./GoogleForms.md): used to upload Vestiges, can be changed client side via the `Upload ID` option
	 - Note that you will have to obtain the relevant entry IDs from the form and also set them accordingly
 - [Google Sheets](./GoogleSheets.md): should be created through the form mentioned above, can be changed client side via the `Download ID` option
	 - The client treats formatting very strictly, manually modifying entries could cause exceptions on all clients or prevent them from loading the csv to begin with
 - [Google Scripts](./VestigeClear.gs): not required, but is used to clear out Vestiges that are older than a month
	 - This script does not clear entries viewable to the owner on the form mentioned above
