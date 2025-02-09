import grpc from 'k6/net/grpc';
import { check, sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';

const client = new grpc.Client();
client.load(['../Grpc.Solution/Protos'], 'validation.proto');

const validationsSent = new Counter('validations_sent');
const validationLatency = new Trend('validation_latency');

export const options = {
    scenarios: {
        constant_load: {
            executor: 'constant-vus',
            vus: 50,
            duration: '5m'
        },
        ramp_up: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '2m', target: 100 },
                { duration: '5m', target: 100 },
                { duration: '2m', target: 0 }
            ]
        }
    },
    thresholds: {
        'validation_latency': ['p(95)<500'],
        'grpc_req_duration': ['p(95)<1000']
    }
};

export default function () {
    const metadata = {
        'equipment-id': `EQ-${__VU}-${__ITER}`
    };

    const validation = {
        equipment_id: metadata['equipment-id'],
        card_id: `CARD-${Math.random().toString(36)}`,
        timestamp: Date.now() * 10000,
        location: 'STATION-1',
        amount: Math.random() * 100,
        result: 0,
        sequence: __ITER,
        session_id: `SESSION-${__VU}`
    };

    const startTime = new Date();
    
    const response = client.invoke('validation.ValidationService/SendValidation', {
        request: validation,
        metadata: metadata
    });

    validationLatency.add(new Date() - startTime);
    validationsSent.add(1);

    check(response, {
        'status is OK': (r) => r && r.status === grpc.StatusOK,
        'response has success': (r) => r && r.message && r.message.success === true
    });

    sleep(1);
}