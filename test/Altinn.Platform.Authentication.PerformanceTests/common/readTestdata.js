/**
 * This file contains the implementation of reading test data from CSV files.
 * The test data includes service owners, end users, and end users with tokens.
 * The data is read using the PapaParse library and stored in SharedArray variables.
 * 
 * @module readTestdata
 */

import papaparse from 'https://jslib.k6.io/papaparse/5.1.1/index.js';
import { SharedArray } from "k6/data";
import exec from 'k6/execution';
//

/**
 * Function to read the CSV file specified by the filename parameter.
 * @param {} filename 
 * @returns 
 */
function readCsv(filename) {
  try {
    return papaparse.parse(open(filename), { header: true, skipEmptyLines: true }).data;
  } catch (error) {
    console.log(`Error reading CSV file: ${error}`);
    return [];
  } 
}

if (!__ENV.API_ENVIRONMENT) {
  throw new Error('API_ENVIRONMENT must be set');
}
const systemUsersFilename = `../testdata/data-${__ENV.API_ENVIRONMENT}-random-customers.csv`;

/**
 * SharedArray variable that stores the service owners data.
 * The data is parsed from the CSV file specified by the filenameServiceowners variable.
 * 
 * @name systemUsers
 * @type {SharedArray}
 */
const systemUsers = new SharedArray('systemUsers', function () {
  return readCsv(systemUsersFilename);
});

export const resources = [ 
  "ttd-dialogporten-performance-test-01", 
  "ttd-dialogporten-performance-test-02", 
  "ttd-dialogporten-performance-test-03", 
  "ttd-dialogporten-performance-test-04", 
  "ttd-dialogporten-performance-test-05", 
  "ttd-dialogporten-performance-test-06", 
  "ttd-dialogporten-performance-test-07", 
  "ttd-dialogporten-performance-test-08", 
  "ttd-dialogporten-performance-test-09", 
  "ttd-dialogporten-performance-test-10"
];

export const regnskapsforerUrns = [
  'urn:altinn:accesspackage:regnskapsforer-med-signeringsrettighet',
  'urn:altinn:accesspackage:regnskapsforer-uten-signeringsrettighet',
  'urn:altinn:accesspackage:regnskapsforer-lonn',
];
export const revisorUrns = [
  'urn:altinn:accesspackage:ansvarlig-revisor',
  'urn:altinn:accesspackage:revisormedarbeider',
];
export const forretningsforerUrns = ['urn:altinn:accesspackage:forretningsforer-eiendom'];

function systemUsersPart(totalVus, vuId) {
  const systemUsersLength = systemUsers.length;
  if (totalVus == 1) {
      return systemUsers.slice(0, systemUsersLength);
  }
  let systemUsersPerVu = Math.floor(systemUsersLength / totalVus);
  let extras = systemUsersLength % totalVus;
  let ixStart = (vuId-1) * systemUsersPerVu;
  if (vuId <= extras) {
      systemUsersPerVu++;
      ixStart += vuId - 1;
  }
  else {
      ixStart += extras;
  }
  return systemUsers.slice(ixStart, ixStart + systemUsersPerVu);
}

export function splitSystemUsers() {
  const totalVus = exec.test.options.scenarios.default.vus;
  let parts = [];
  for (let i = 1; i <= totalVus; i++) {
        parts.push(systemUsersPart(totalVus, i));
  }
  return parts;
}


