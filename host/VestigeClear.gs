//Code based on https://support.google.com/docs/thread/101625681?hl=en&msgid=102898478
function VestigeClear() {
	// set cutoff date
	const cutoffDate = new Date();
	cutoffDate.setMonth(cutoffDate.getMonth() - 1);
	
	const spreadsheetId = '1mUk-KQp7Kv4U-ODamQwb7DUWNewvyXLucVu72bVqFZU';
	const sheet = SpreadsheetApp.openById(spreadsheetId);
	const dataValues = sheet.getSheetValues(2,1,-1,1).flat();
	const len = dataValues.length;

	for (let i = 0; i < len; i++) {
		if (Date.parse(dataValues[i]) >= cutoffDate) {
			if (i != 0) {
				sheet.deleteRows(2, i);
				Logger.log("Cleared " + i + " outdated vestiges.")
			}
			i = len;
		}
	}
}
