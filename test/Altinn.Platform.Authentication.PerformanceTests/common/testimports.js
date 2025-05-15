export { uuidv4, randomItem, randomIntBetween } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';
export { URL } from 'https://jslib.k6.io/url/1.0.0/index.js';
export { 
    getPersonalToken, 
    getEnterpriseToken,
    chai, 
    expect, 
    expectStatusFor,
    describe,
    customConsole
} from 'https://raw.githubusercontent.com/dagfinno/altinn-platform/refs/heads/main/libs/k6/src/index.js';