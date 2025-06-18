import papaparse from 'https://jslib.k6.io/papaparse/5.1.1/index.js';
import { SharedArray } from "k6/data";

const endUsersFilename = `../testdata/data-${__ENV.API_ENVIRONMENT}-end-users.csv`;
const siUsersFilename = `../testdata/data-${__ENV.API_ENVIRONMENT}-si-users.csv`;
const organizationsFilename = `../testdata/data-${__ENV.API_ENVIRONMENT}-organizations.csv`;

function readCsv(filename) {
    try {
      return papaparse.parse(open(filename), { header: true, skipEmptyLines: true }).data;
    } catch (error) {
      console.log(`Error reading CSV file: ${error}`);
      return [];
    } 
  }

export const endUsers = new SharedArray('endUsers', function () {
    const csv = readCsv(endUsersFilename);
    console.log(`Read ${csv.length} end users from ${endUsersFilename}`);
    return csv;
});
  
export const siUsers = new SharedArray('siUsers', function () {
    return readCsv(siUsersFilename);
});
  
export const organizations = new SharedArray('organizations', function () {
    return readCsv(organizationsFilename);
});