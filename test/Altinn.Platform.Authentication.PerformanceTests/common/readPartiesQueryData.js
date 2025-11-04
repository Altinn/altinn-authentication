import papaparse from 'https://jslib.k6.io/papaparse/5.1.1/index.js';
import { SharedArray } from "k6/data";
import { environment } from './config.js';

const endUsersFilename = `../testdata/data-${environment}-end-users.csv`;
const siUsersFilename = `../testdata/data-${environment}-si-users.csv`;
const organizationsFilename = `../testdata/data-${environment}-organizations.csv`;
const orgsWithCustomersFilename = `../testdata/data-${environment}-all-customers.csv`;

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

export const orgsWithCustomers = new SharedArray('orgsWithCustomers', function () {
  return readCsv(orgsWithCustomersFilename);
});

