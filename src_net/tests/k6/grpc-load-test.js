import grpc from 'k6/net/grpc';
import { check, sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';

const client = new grpc.Client();
client.load(['../Grpc.Solution/Protos'], 'validation.proto');

const validationsSent = new Counter('validations_sent');
const validationLatency = new Trend('validation_latency');
const streamLatency = new Trend('stream_latency');
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
        'stream_latency': ['p(95)<1000'],
        'batch_latency': ['p(95)<2000'],
        'grpc_req_duration': ['p(95)<1000']
    }
};

function createValidationRequest() {
    return {
        equipment_id: `EQ-${__VU}-${__ITER}`,
        token_id: `TOKEN-${Math.random().toString(36)}`,
        timestamp: {
            seconds: Math.floor(Date.now() / 1000),
            nanos: (Date.now() % 1000) * 1000000
        },
        location: 'STATION-1',
        amount: Math.random() * 100,
        type: 'ENTRY',
        status: 'PENDING',
        sequence: __ITER,
        session_id: `SESSION-${__VU}`,
        metadata: {
            device_type: 'VALIDATOR',
            firmware_version: '1.0.0',
            line_id: 'LINE-1'
        }
    };
}

export default function () {
    const metadata = {
        'equipment-id': `EQ-${__VU}-${__ITER}`
    };

    // Single validation test
    {
        const startTime = new Date();
        const response = client.invoke('validation.Validator/ValidateSingle', {
            request: createValidationRequest(),
            metadata: metadata
        });

        validationLatency.add(new Date() - startTime);
        validationsSent.add(1);

        check(response, {
            'single validation status is OK': (r) => r && r.status === grpc.StatusOK,
            'single validation response has success': (r) => r && r.message && r.message.success === true
        });
    }

    // Batch validation test
    {
        const batchSize = 10;
        const requests = Array(batchSize).fill(null).map(() => createValidationRequest());
        const batchRequest = {
            validations: requests,
            batch_id: `BATCH-${__VU}-${__ITER}`
        };

        const startTime = new Date();
        const response = client.invoke('validation.Validator/ValidateBatch', {
            request: batchRequest,
            metadata: metadata
        });

        batchLatency.add(new Date() - startTime);
        validationsSent.add(batchSize);

        check(response, {
            'batch validation status is OK': (r) => r && r.status === grpc.StatusOK,
            'batch processed all records': (r) => r && r.message && r.message.processed_count === batchSize,
            'batch has no failures': (r) => r && r.message && r.message.failed_validation_ids.length === 0
        });
    }

    sleep(1);
}