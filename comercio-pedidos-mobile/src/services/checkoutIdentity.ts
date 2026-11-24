import {getRandomValues} from 'expo-crypto';

function randomBytes(length:number):Uint8Array {
 return getRandomValues(new Uint8Array(length));
}

function hex(bytes:Uint8Array):string {
 return Array.from(bytes,byte=>byte.toString(16).padStart(2,'0')).join('');
}

// Web Crypto randomUUID requires a secure context; getRandomValues also works
// on the HTTP LAN origin used for mobile browser testing. Both platforms keep
// cryptographic randomness; never fall back to Math.random for checkout tokens.
export function newRequestId():string {
 const bytes=randomBytes(16);
 bytes[6]=(bytes[6]&0x0f)|0x40;
 bytes[8]=(bytes[8]&0x3f)|0x80;
 const value=hex(bytes);
 return `${value.slice(0,8)}-${value.slice(8,12)}-${value.slice(12,16)}-${value.slice(16,20)}-${value.slice(20)}`;
}

export function newToken():string {
 return hex(randomBytes(32));
}
