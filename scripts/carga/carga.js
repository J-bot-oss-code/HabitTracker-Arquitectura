// Prueba de carga de HabitTracker Nexus (k6), versión de régimen estable.
// Las personas entran de forma GRADUAL (RITMO nuevos usuarios por segundo, como llegan en la vida real) e inician sesión una vez; luego
// consultan su día, su progreso y sus avisos con pausas de 3 a 8 s y guardan algo de vez en cuando. La latencia que importa es la de
// la fase «estable» (todas ya conectadas); la de «subida» se mide aparte. Variables: BASE, USUARIOS, CLAVE, VUS, RITMO, ESTABLE (s).
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';

const BASE = __ENV.BASE;
const USUARIOS = (__ENV.USUARIOS || '').split(',').filter(Boolean);
const CLAVE = __ENV.CLAVE;
const VUS = parseInt(__ENV.VUS || '200');
const RITMO = parseFloat(__ENV.RITMO || '1.5');
const ESTABLE = parseInt(__ENV.ESTABLE || '120');
const RAMPA = Math.ceil(VUS / RITMO);
const T0 = Date.now();
const FIN = T0 + (RAMPA + ESTABLE) * 1000;

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: {
    personas: {
      executor: 'ramping-vus',
      stages: [{ duration: `${RAMPA}s`, target: VUS }, { duration: `${ESTABLE}s`, target: VUS }],
      gracefulStop: '20s',
    },
  },
  thresholds: {
    'http_req_duration{tipo:lectura,fase:estable}': ['p(95)<800'],
    'http_req_duration{tipo:escritura,fase:estable}': ['p(95)<800'],
    'errores_estables': ['rate<0.01'],
  },
};

const tLogin = new Trend('login_ms', true);
const errores = new Rate('errores_estables');
const cuerpoPerfil = JSON.stringify({ perfilLaboral: 'OficinaDiurna', zonaHoraria: 'America/Santo_Domingo', avisoSaludAceptado: true });
const pausa = () => sleep(3 + Math.random() * 5);
const fase = () => (Date.now() - T0 > RAMPA * 1000 ? 'estable' : 'subida');

export default function () {
  const correo = USUARIOS[(__VU - 1) % USUARIOS.length];
  const login = http.post(`${BASE}/api/auth/login`, JSON.stringify({ email: correo, password: CLAVE }), {
    headers: { 'Content-Type': 'application/json' }, tags: { tipo: 'login' },
  });
  tLogin.add(login.timings.duration);
  if (!check(login, { 'login 200': (r) => r.status === 200 })) { sleep(5); return; }
  const cab = { headers: { Authorization: `Bearer ${login.json('token')}`, 'Content-Type': 'application/json' } };
  const t = (tipo) => ({ ...cab, tags: { tipo, fase: fase() } });

  let i = 0;
  while (Date.now() < FIN) {
    const f = fase(); const cuenta = f === 'estable';
    let r = http.get(`${BASE}/api/today`, t('lectura')); if (cuenta) errores.add(r.status !== 200); pausa();
    r = http.get(`${BASE}/api/progress/summary`, t('lectura')); if (cuenta) errores.add(r.status !== 200); pausa();
    if (i % 3 === 0) { r = http.get(`${BASE}/api/notifications`, t('lectura')); if (cuenta) errores.add(r.status !== 200); pausa(); }
    if (i % 4 === 0) { r = http.get(`${BASE}/api/plans`, t('lectura')); if (cuenta) errores.add(r.status !== 200); pausa(); }
    if (i % 6 === 0) { r = http.put(`${BASE}/api/profile`, cuerpoPerfil, t('escritura')); if (cuenta) errores.add(r.status !== 200); pausa(); }
    i++;
  }
}
