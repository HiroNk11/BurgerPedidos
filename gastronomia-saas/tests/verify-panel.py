import urllib.request,json,http.cookiejar,re,sys
from pathlib import Path
base=sys.argv[1].rstrip('/') if len(sys.argv)>1 else 'http://localhost:5180'
access_file=sys.argv[2] if len(sys.argv)>2 else 'outputs/Panel-acceso-local.md'
opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
def request(path,method='GET',body=None,csrf=None):
 headers={}
 if body is not None:headers['Content-Type']='application/json'
 if csrf:headers['X-CSRF-TOKEN']=csrf
 try:
  res=opener.open(urllib.request.Request(base+path, data=json.dumps(body).encode() if body is not None else None,headers=headers,method=method)); data=res.read();return res.status,json.loads(data) if data else None
 except urllib.error.HTTPError as e:
  data=e.read()
  try: data=json.loads(data)
  except: data={}
  return e.code,data
code,s=request('/api/staff/session'); assert code==200 and not s['authenticated']
print('PASS sesión anónima sin acceso a pedidos')
creds=Path(access_file).read_text(encoding='utf-8');password=re.search(r'Contraseña: `([^`]+)`',creds).group(1)
code,_=request('/api/staff/login','POST',{'email':'admin@burgerclub.local','password':password},s['csrfToken']);assert code==204,(code,'login failed')
code,s=request('/api/staff/session');assert code==200 and s['authenticated'];tid=s['tenantId'];csrf=s['csrfToken'];root='/api/tenants/'+tid
code,data=request(root+'/staff/orders');assert code==200 and data['total']==6
print('PASS acceso con contraseña y listado SQL de 6 pedidos')
code,_=request('/api/tenants/00000000-0000-0000-0000-000000000001/staff/orders');assert code==403,code
print('PASS aislamiento por comercio HTTP')
item=next(i for i in data['items'] if i['estadoPedido']=='PendienteConfirmacion')
code,_=request(root+'/orders/'+item['id']+'/accept?iniciarPreparacion=false','POST');assert code==400,code
print('PASS mutación sin token CSRF rechazada')
code,summary=request(root+'/staff/orders/summary');assert code==200 and summary['pendientes']>=1
print('PASS resumen de cobros y cocina')
code,filtered=request(root+'/staff/orders?soloPendientesPago=true');assert code==200 and all(i['estadoPedido']!='Cancelado' and i['estadoPago']!='Pagado' for i in filtered['items'])
print('PASS cobros pendientes excluye cancelados y pagados')
code,_=request('/api/staff/logout','POST',{},csrf);assert code==204
code,_=request(root+'/staff/orders');assert code==401,code
print('PASS logout impide acceso a datos privados')
code,_=request(root+'/orders','POST',{'requestId':'00000000-0000-0000-0000-000000000001','guestEmail':'invalid','guestPhone':'bad','metodoPago':'Efectivo','items':[{'productId':'00000000-0000-0000-0000-000000000000','cantidad':0}]});assert code==400,code
print('PASS validación HTTP de DTO inválido devuelve 400, no 500')
