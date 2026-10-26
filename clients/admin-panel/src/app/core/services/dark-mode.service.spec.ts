import { TestBed } from '@angular/core/testing';
import { DarkModeService } from './dark-mode.service';

describe('DarkModeService preference',()=>{
  let oldTheme:string|null;let oldDark:boolean;
  beforeEach(()=>{oldTheme=localStorage.getItem('theme');oldDark=document.documentElement.classList.contains('dark');});
  afterEach(()=>{if(oldTheme===null)localStorage.removeItem('theme');else localStorage.setItem('theme',oldTheme);document.documentElement.classList.toggle('dark',oldDark);});
  it('restores a saved theme and keeps the document, signal and next session aligned',()=>{
    localStorage.setItem('theme','dark');const theme=TestBed.inject(DarkModeService);
    expect(theme.isDarkMode()).toBe(true);expect(document.documentElement.classList.contains('dark')).toBe(true);
    theme.toggle();expect(theme.isDarkMode()).toBe(false);expect(document.documentElement.classList.contains('dark')).toBe(false);expect(localStorage.getItem('theme')).toBe('light');
    TestBed.resetTestingModule();expect(TestBed.inject(DarkModeService).isDarkMode()).toBe(false);
  });
});
