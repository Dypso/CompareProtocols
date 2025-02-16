import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';

const validationsSent = new Counter('validations_sent');
const validationLatency = new Trend('validation_latency');
const batchLatency = new Trend('batch_latency');

export const options = {
    scenarios: {
        single_validations: {
            executor: 'constant-vus',
            vus: 50,
            duration: '5m'
        },
        batch_validations: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '2m', target: 50 },
                { duration: '5m', target: 50 },
                { duration: '2m', target: 0 }
            ]
        }
    },
    thresholds: {
        'validation_latency': ['p(95)<500'],
        'batch_latency': ['p(95)<2000'],
        'http_req_duration': ['p(95)<1000']
    }
};

const baseUrl = 'https://localhost:5001';
const params = {
    headers: {
        'Content-Type': 'application/json',
    },
    http2: true,
};

function createValidationRequest() {
    return {
        equipmentId: `EQ-${__VU}-${__ITER}`,
        tokenId: `TOKEN-${Math.random().toString(36)}`,
        timestamp: new Date().toISOString(),
        location: 'STATION-1',
        amount: Math.random() * 100,
        type: 'ENTRY',
        status: 'PENDING',
        sequence: __ITER,
        sessionId: `SESSION-${__VU}`,
        metadata: {
            deviceType: 'VALIDATOR',
            firmwareVersion: '1.0.0',
            lineId: 'LINE-1'
        }
    };
}

export default function () {
    // Single validation test
    {
        const validationRequest = createValidationRequest();
        const startTime = new Date();
        
        const response = http.post(
            `${baseUrl}/api/v1/validations`, 
            JSON.stringify(validationRequest), 
            params
        );

        validationLatency.add(new Date() - startTime);
        validationsSent.add(1);

        check(response, {
            'single validation status is 200': (r) => r.status === 200,
            'single validation response has success': (r) => {
                const body = JSON.parse(r.body);
                return body && body.success === true;
            }
        });
    }

    // Batch validation test
    {
        const batchSize = 10;
        const requests = Array(batchSize).fill(null).map(() => createValidationRequest());
        const batchRequest = {
            validations: requests,
            batchId: `BATCH-${__VU}-${__ITER}`
        };

        const startTime = new Date();
        const response = http.post(
            `${baseUrl}/api/v1/validations/batch`, 
            JSON.stringify(batchRequest), 
            params
        );

        batchLatency.add(new Date() - startTime);
        validationsSent.add(batchSize);

        check(response, {
            'batch validation status is 200': (r) => r.status === 200,
            'batch validation response is valid': (r) => {
                const body = JSON.parse(r.body);
                return body && 
                    body.processedCount === batchSize && 
                    body.failedValidationIds.length === 0;
            }
        });
    }

    sleep(1);
}