# Google Form Layout

The form must not require users to log in and needs to output the data to [google sheets](./GoogleSheets.md).

All questions below should be set to required:

 - Short Answer
	 - Name/Title: room
	 - Response validation:
		 - Regular Expression
		 - Matches
		 - `^[A-Za-z0-9]{1,25}$`
 - Short Answer
	 - Name/Title: region
	 - Response validation:
		 - Regular Expression
		 - Matches
		 - `^[A-Za-z]{1,25}_[A-Za-z0-9_]{1,50}$`
 - Short Answer
	 - Name/Title: colour.r
	 - Response validation:
		 - Number
		 - Between `1` and `0`
 - Short Answer
	 - Name/Title: colour.g
	 - Response validation:
		 - Number
		 - Between `1` and `0`
 - Short Answer
	 - Name/Title: colour.b
	 - Response validation:
		 - Number
		 - Between `1` and `0`
 - Short Answer
	 - Name/Title: spawn.x
	 - Response validation:
		 - Number
		 - Whole number
 - Short Answer
	 - Name/Title: spawn.y
	 - Response validation:
		 - Number
		 - Whole number
 - Short Answer
	 - Name/Title: target.x
	 - Response validation:
		 - Number
		 - Whole number
 - Short Answer
	 - Name/Title: target.y
	 - Response validation:
		 - Number
		 - Whole number
