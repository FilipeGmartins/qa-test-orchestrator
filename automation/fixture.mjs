import http from 'node:http';
http.createServer((_req, res) => {
  res.writeHead(200, { 'Content-Type': 'text/html' });
  res.end('<!doctype html><html><head><title>QA Runner Fixture</title></head><body><h1>Alvo local de validação do runner</h1></body></html>');
}).listen(5180, '127.0.0.1', () => console.log('Fixture: http://127.0.0.1:5180/'));
