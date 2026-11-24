import {defineConfig} from 'vite';
export default defineConfig({base:'/admin/',build:{outDir:'../src/Gastronomia.Api/wwwroot/admin',emptyOutDir:true},server:{proxy:{'/api':'http://localhost:5180'}}});